namespace System.IO.Compression2
{
    using System.Diagnostics;

    internal enum CompressionTracingSwitchLevel
    {
        Off = 0,
        Informational = 1,
        Verbose = 2
    }

    internal class CompressionTracingSwitch : Switch
    {
        internal static CompressionTracingSwitch tracingSwitch =
            new CompressionTracingSwitch("CompressionSwitch", "Compression Library Tracing Switch");

        internal CompressionTracingSwitch(string displayName, string description)
            : base(displayName, description)
        {
        }

        public static bool Verbose
        {
            get
            {
                return tracingSwitch.SwitchSetting >= (int)CompressionTracingSwitchLevel.Verbose;
            }
        }

        public static bool Informational
        {
            get
            {
                return tracingSwitch.SwitchSetting >= (int)CompressionTracingSwitchLevel.Informational;
            }
        }

#if ENABLE_TRACING
        public void SetSwitchSetting(CompressionTracingSwitchLevel level) {
            if (level < CompressionTracingSwitchLevel.Off || level > CompressionTracingSwitchLevel.Verbose) {
                throw new ArgumentOutOfRangeException("level");
            }
            this.SwitchSetting = (int)level;
        }
#endif

    }
}

