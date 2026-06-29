using System;
using System.IO;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Dalamud.CharacterSync.Interface
{
    /// <summary>
    /// Main configuration window.
    /// </summary>
    internal class ConfigWindow : Window
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigWindow"/> class.
        /// </summary>
        public ConfigWindow()
            : base("Character Sync Config", ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar)
        {
            this.Size = new Vector2(750, 520);
        }

        /// <inheritdoc/>
        public override void Draw()
        {
            if (CharacterSyncPlugin.NeedsRestart)
            {
                var warningMin = ImGui.GetCursorScreenPos();
                var warningMax = new System.Numerics.Vector2(
                    warningMin.X + ImGui.GetContentRegionAvail().X,
                    warningMin.Y + 36);
                ImGui.GetWindowDrawList().AddRectFilled(warningMin, warningMax, 0xFF0070CC);
                ImGui.SetCursorScreenPos(new System.Numerics.Vector2(warningMin.X + 8, warningMin.Y + 9));
                ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(1f, 1f, 1f, 1f));
                ImGui.TextUnformatted("⚠  Sync is disabled — restart your game for the plugin to take effect.");
                ImGui.PopStyleColor();
                ImGui.SetCursorScreenPos(new System.Numerics.Vector2(warningMin.X, warningMax.Y + 4));
                ImGui.Spacing();
            }

            ImGui.PushTextWrapPos();
            ImGui.Text("This window allows you to configure Character Sync.");
            ImGui.Text("Click the button below while being logged in on your main character - all logins from now on will use this character's save data!");
            ImGui.Text("None of your save data will be modified.");
            ImGui.Text("Please note that it is recommended to restart your game after changing these settings.");
            ImGui.PopTextWrapPos();
            ImGui.Separator();

            if (!Service.PlayerState.IsLoaded)
            {
                ImGui.Text("Please log in before using this plugin.");
                return;
            }

            if (ImGui.Button("Set save data to current character"))
            {
                Service.Configuration.Cid = Service.PlayerState.ContentId;
                Service.Configuration.SetName =
                    $"{Service.PlayerState.CharacterName} on {Service.PlayerState.HomeWorld.Value.Name}";
                Service.Configuration.Save();
                Service.PluginLog.Info("CS saved.");
            }

            if (Service.Configuration.Cid == 0)
            {
                ImGui.Text("No character was set as main character yet.");
                ImGui.Text("Please click the button above while being logged in on your main character.");
            }
            else
            {
                ImGui.Text($"Currently set to {Service.Configuration.SetName}(FFXIV_CHR{Service.Configuration.Cid:X16})");
            }

            ImGui.Dummy(new Vector2(5, 5));

            ImGui.Text(
                $"The logged in character is {Service.PlayerState.CharacterName} on {Service.PlayerState.HomeWorld.Value.Name}(FFXIV_CHR{Service.PlayerState.ContentId:X16}");
            ImGui.Dummy(new Vector2(20, 20));

            ImGui.Text("Backups to keep:");
            ImGui.SameLine();
            ImGui.InputInt(string.Empty, ref Service.Configuration.BackupCount, 1, 10);
            if (ImGui.Button("Open Backups Folder"))
            {
                var path = Path.Combine(Service.Interface.GetPluginConfigDirectory(), "backups");
                try
                {
                    Directory.CreateDirectory(path);
                    Utility.Util.OpenLink(path);
                }
                catch (Exception e)
                {
                    Service.PluginLog.Error(e, "Failed to open backups folder.");
                }
            }

            ImGui.Checkbox("Sync Hotbars", ref Service.Configuration.SyncHotbars);
            ImGui.Checkbox("Sync Macros", ref Service.Configuration.SyncMacro);
            ImGui.Checkbox("Sync Keybinds", ref Service.Configuration.SyncKeybind);
            ImGui.Checkbox("Sync Chatlog Settings", ref Service.Configuration.SyncLogfilter);
            ImGui.Checkbox("Sync Character Settings", ref Service.Configuration.SyncCharSettings);
            ImGui.Checkbox("Sync Keyboard Settings", ref Service.Configuration.SyncKeyboardSettings);
            ImGui.Checkbox("Sync Gamepad Settings", ref Service.Configuration.SyncGamepadSettings);
            ImGui.Checkbox("Sync Card Sets and Verminion Settings", ref Service.Configuration.SyncCardSets);
            ImGui.Checkbox("Sync Command Panel", ref Service.Configuration.SyncCommandPanel);

            ImGui.Separator();

            if (ImGui.Button("Save"))
            {
                this.IsOpen = false;
                Service.Configuration.Save();
                Service.PluginLog.Information("CS saved.");
            }
        }
    }
}
