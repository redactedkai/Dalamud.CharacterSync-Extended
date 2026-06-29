using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using FFXIVClientStructs.FFXIV.Client.UI.Misc;

namespace Dalamud.CharacterSync
{
    /// <summary>
    /// Syncs the Command Panel (QPNL segment in UISAVE.DAT) from the main character to alts at login.
    /// Uses a permanent write via UiSavePackModule.SaveFile(true) rather than an in-memory-only sync.
    /// </summary>
    internal sealed class CommandPanelSync : IDisposable
    {
        private const int CurrentVersion = 2;
        private const int DataSize = 0x228; // sizeof(QuickPanelModule) - sizeof(UserFileManager.UserFileEvent)

        private ulong _loggedInCid;

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandPanelSync"/> class.
        /// </summary>
        public CommandPanelSync()
        {
            Service.ClientState.Login += this.OnLogin;
            Service.ClientState.Logout += this.OnLogout;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Service.ClientState.Login -= this.OnLogin;
            Service.ClientState.Logout -= this.OnLogout;
        }

        // Login/Logout events fire on the framework thread, so memory reads here are safe.
        private void OnLogin()
        {
            this._loggedInCid = Service.PlayerState.ContentId;

            if (!Service.Configuration.SyncCommandPanel || Service.Configuration.Cid == 0)
                return;

            if (this._loggedInCid == Service.Configuration.Cid)
            {
                SaveShared();
            }
            else if (File.Exists(SharedPath()))
            {
                LoadAndApplyShared();
            }
        }

        private void OnLogout(int type, int code)
        {
            if (!Service.Configuration.SyncCommandPanel || Service.Configuration.Cid == 0)
                return;

            if (this._loggedInCid == Service.Configuration.Cid)
            {
                // Capture any in-session edits the main character made.
                SaveShared();
            }
        }

        // Reads QPNL bytes synchronously on the framework thread, then writes to disk off-thread.
        private static void SaveShared()
        {
            unsafe
            {
                var ptr = GetQpnlPointer();
                if (ptr == 0) return;

                var bytes = new byte[DataSize];
                Marshal.Copy(ptr, bytes, 0, DataSize);
                var path = SharedPath();

                _ = Task.Run(() =>
                {
                    try
                    {
                        File.WriteAllBytes(path, bytes);
                    }
                    catch (Exception ex)
                    {
                        Service.PluginLog.Error(ex, "CommandPanelSync: failed to save shared snapshot");
                    }
                });
            }
        }

        // Reads shared snapshot off-thread, then writes into game memory and force-saves on the framework thread.
        private static void LoadAndApplyShared()
        {
            var path = SharedPath();
            _ = Task.Run(async () =>
            {
                try
                {
                    var bytes = await File.ReadAllBytesAsync(path);
                    if (bytes.Length != DataSize)
                    {
                        Service.PluginLog.Warning(
                            $"CommandPanelSync: unexpected snapshot size {bytes.Length}, expected {DataSize}");
                        return;
                    }

                    await Service.Framework.RunOnFrameworkThread(() =>
                    {
                        unsafe
                        {
                            var ptr = GetQpnlPointer();
                            if (ptr == 0) return;

                            Marshal.Copy(bytes, 0, ptr, DataSize);

                            // Mirrors VanillaPlus HUDPresetManager.LoadPreset() — force-saves UISAVE.DAT
                            // so the change is written permanently to the alt's character file.
                            var savePackModule = UiSavePackModule.Instance();
                            if (savePackModule != null)
                                savePackModule->SaveFile(true);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Service.PluginLog.Error(ex, "CommandPanelSync: failed to apply shared snapshot");
                }
            });
        }

        private static unsafe nint GetQpnlPointer()
        {
            var module = QuickPanelModule.Instance();
            return module == null ? 0 : (nint)module + sizeof(UserFileManager.UserFileEvent);
        }

        private static string SharedPath()
        {
            var dir = Path.Combine(Service.Interface.GetPluginConfigDirectory(), "CommandPanelSync");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, $"Shared.v{CurrentVersion}.qpnl.dat");
        }
    }
}
