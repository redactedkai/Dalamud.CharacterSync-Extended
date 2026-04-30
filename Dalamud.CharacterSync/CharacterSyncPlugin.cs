using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

using Dalamud.CharacterSync.Config;
using Dalamud.CharacterSync.Interface;
using Dalamud.Game.Command;
using Dalamud.Hooking;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Dalamud.CharacterSync
{
    /// <summary>
    /// Main plugin class.
    /// </summary>
    internal class CharacterSyncPlugin : IDalamudPlugin
    {
        private readonly WindowSystem windowSystem;
        private readonly ConfigWindow configWindow;
        private readonly Regex saveFolderRegex = new(
            @"(?<path>.*)FFXIV_CHR(?<cid>.*)\/(?!ITEMODR\.DAT|ITEMFDR\.DAT|GEARSET\.DAT|UISAVE\.DAT|.*\.log)(?<dat>.*)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private Hook<FileInterfaceOpenFileDelegate>? openFileHook;

        /// <summary>
        /// Initializes a new instance of the <see cref="CharacterSyncPlugin"/> class.
        /// </summary>
        /// <param name="interf">PluginInterface from dalamud.</param>
        /// <param name="logger">Dalamud provided logger for the plugin.</param>
        public CharacterSyncPlugin(IDalamudPluginInterface interf, IPluginLog logger)
        {
            interf.Create<Service>();
            Service.PluginLog = logger;

            Service.Configuration = Service.Interface.GetPluginConfig() as CharacterSyncConfig ?? new CharacterSyncConfig();

            this.configWindow = new();
            this.windowSystem = new("CharacterSync");
            this.windowSystem.AddWindow(this.configWindow);

            Service.Interface.UiBuilder.Draw += this.windowSystem.Draw;
            Service.Interface.UiBuilder.OpenConfigUi += this.OnOpenConfigUi;

            Service.CommandManager.AddHandler("/pcharsync", new CommandInfo(this.OnChatCommand)
            {
                HelpMessage = "Open the Character Sync configuration.",
                ShowInHelp = true,
            });

            Service.PluginLog.Information($"Load Reason: {Service.Interface.Reason}");
            switch (Service.Interface.Reason)
            {
                case PluginLoadReason.Boot:
                    this.AttemptBackup();
                    this.EnableFunctionality();
                    break;
                case PluginLoadReason.Installer:
                    Service.NotificationManager.AddNotification(new Notification
                    {
                        Content = "Character Data Sync has been installed, but it won't do anything until you configure it and restart your game. Please use /pcharsync to access the settings.",
                        MinimizedText = "Setup needed!",
                        Type = NotificationType.Warning,
                        InitialDuration = TimeSpan.MaxValue,
                        ShowIndeterminateIfNoExpiry = false,
                    });
                    break;
                case PluginLoadReason.Update:
                    Service.NotificationManager.AddNotification(new Notification
                    {
                        Content = "Character Data Sync has been updated. It will not function until you restart your game.",
                        MinimizedText = "Restart required!",
                        Type = NotificationType.Warning,
                        InitialDuration = TimeSpan.FromSeconds(60),
                    });
                    break;
                default:
                    Service.NotificationManager.AddNotification(new Notification
                    {
                        Content = "Character Data Sync has been loaded in the middle of gameplay so it has automatically disabled itself. It will not function until you restart your game.",
                        MinimizedText = "Restart required!",
                        Type = NotificationType.Warning,
                        InitialDuration = TimeSpan.FromSeconds(60),
                    });
                    break;
            }
        }

        private delegate IntPtr FileInterfaceOpenFileDelegate(
            IntPtr pFileInterface,
            [MarshalAs(UnmanagedType.LPWStr)] string filepath, // IntPtr pFilepath
            uint a3);

        /// <summary>
        /// Gets the plugin name.
        /// </summary>
        public string Name => "Character Sync";

        /// <inheritdoc/>
        public void Dispose()
        {
            Service.CommandManager.RemoveHandler("/pcharsync");
            Service.Interface.UiBuilder.Draw -= this.windowSystem.Draw;
            this.openFileHook?.Dispose();
        }

        private void OnOpenConfigUi()
        {
            this.configWindow.Toggle();
        }

        private void OnChatCommand(string command, string arguments)
        {
            this.configWindow.Toggle();
        }

        private void AttemptBackup()
        {
            try
            {
                var configFolder = Service.Interface.GetPluginConfigDirectory();
                Directory.CreateDirectory(configFolder);

                var backupFolder = new DirectoryInfo(Path.Combine(configFolder, "backups"));
                Directory.CreateDirectory(backupFolder.FullName);

                var folders = backupFolder.GetDirectories().OrderBy(x => long.Parse(x.Name)).ToArray();
                if (folders.Length > Service.Configuration.BackupCount)
                {
                    folders.FirstOrDefault()?.Delete(true);
                }

                var thisBackupFolder = Path.Combine(backupFolder.FullName, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
                Directory.CreateDirectory(thisBackupFolder);

                var xivFolderPath = string.Empty;
                unsafe
                {
                    var framework = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance();
                    if (framework is not null)
                    {
                        xivFolderPath = framework->UserPathString;
                    }
                }

                var xivFolder = new DirectoryInfo(xivFolderPath);

                if (!xivFolder.Exists)
                {
                    Service.PluginLog.Error("Could not find XIV folder.");
                    return;
                }

                foreach (var directory in xivFolder.GetDirectories("FFXIV_CHR*"))
                {
                    var thisBackupFile = Path.Combine(thisBackupFolder, directory.Name);
                    Service.PluginLog.Information(thisBackupFile);
                    Directory.CreateDirectory(thisBackupFile);

                    foreach (var filePath in directory.GetFiles("*.DAT"))
                    {
                        File.Copy(filePath.FullName, filePath.FullName.Replace(directory.FullName, thisBackupFile), true);
                    }
                }

                Service.PluginLog.Information("Backup OK!");
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex, $"Could not backup character data files.\n{ex.Message}");
            }
        }

        private void EnableFunctionality()
        {
            var address = new PluginAddressResolver();
            address.Setup(Service.Scanner);

            this.openFileHook = Service.Interop.HookFromAddress<FileInterfaceOpenFileDelegate>(address.FileInterfaceOpenFileAddress, this.OpenFileDetour);
            this.openFileHook.Enable();
        }

        private IntPtr OpenFileDetour(IntPtr pFileInterface, [MarshalAs(UnmanagedType.LPWStr)] string filepath, uint a3)
        {
            try
            {
                if (Service.Configuration.Cid != 0)
                {
                    var match = this.saveFolderRegex.Match(filepath);
                    if (match.Success)
                    {
                        var rootPath = match.Groups["path"].Value;
                        var datName = match.Groups["dat"].Value;

                        if (this.PerformRewrite(datName))
                        {
                            filepath = $"{rootPath}FFXIV_CHR{Service.Configuration.Cid:X16}/{datName}";
                            Service.PluginLog.Debug("REWRITE: " + filepath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex, "ERROR in OpenFileDetour");
            }

            return this.openFileHook!.Original(pFileInterface, filepath, a3);
        }

        private bool PerformRewrite(string datName)
        {
            switch (datName)
            {
                case "HOTBAR.DAT" when Service.Configuration.SyncHotbars:
                case "MACRO.DAT" when Service.Configuration.SyncMacro:
                case "KEYBIND.DAT" when Service.Configuration.SyncKeybind:
                case "LOGFLTR.DAT" when Service.Configuration.SyncLogfilter:
                case "COMMON.DAT" when Service.Configuration.SyncCharSettings:
                case "CONTROL0.DAT" when Service.Configuration.SyncKeyboardSettings:
                case "CONTROL1.DAT" when Service.Configuration.SyncGamepadSettings:
                case "GS.DAT" when Service.Configuration.SyncCardSets:
                case "ADDON.DAT":
                    return true;
            }

            return false;
        }
    }
}