using System;

using Dalamud.Game;
using Dalamud.Plugin.Services;

namespace Dalamud.CharacterSync
{
    /// <summary>
    /// Plugin address resolver.
    /// </summary>
    internal class PluginAddressResolver : BaseAddressResolver
    {
        // the "0F 85 1C 04 00 00" part of the signature is a jnz, likely to change if logic of function or compiler used changes
        private const string FileInterfaceOpenFileSignature = "E8 ?? ?? ?? ?? 3C 01 0F 85 1C 04 00 00";

        /// <summary>
        /// Gets the address of the FileInterface::OpenFile method.
        /// </summary>
        public IntPtr FileInterfaceOpenFileAddress { get; private set; }

        /// <inheritdoc/>
        protected override void Setup64Bit(ISigScanner scanner)
        {
            this.FileInterfaceOpenFileAddress = scanner.ScanText(FileInterfaceOpenFileSignature);

            Service.PluginLog.Verbose("===== CHARACTER SYNC =====");
            Service.PluginLog.Verbose($"{nameof(this.FileInterfaceOpenFileAddress)} {this.FileInterfaceOpenFileAddress:X}");
        }
    }
}
