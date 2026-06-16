using System;
using Microsoft.Diagnostics.Symbols;
using Xunit;
using Xunit.Abstractions;

using static Microsoft.Diagnostics.Symbols.NativeSymbolModule.MicrosoftPdbSourceFile;

namespace TraceEventTests
{
    /// <summary>
    /// Tests for <see cref="NativeSymbolModule.MicrosoftPdbSourceFile.TryCreateSafeSourceServerCommand"/>,
    /// the per-tool allow-list rewriter that turns a PDB-supplied SRCSRVCMD / TFS_EXTRACT_CMD into a safe
    /// command line we can run directly (no shell).  Malicious PDBs must not be able to embed shell
    /// metacharacters, unknown executables, or unrecognized arguments and have them executed.
    /// </summary>
    public class SourceServerCommandTests : TestBase
    {
        public SourceServerCommandTests(ITestOutputHelper output) : base(output)
        {
        }

        private const string TfExe = @"C:\tools\tf.exe";
        private const string OutputPath = @"C:\cache\abcdef0123456789abcdef0123456789\file.cs";

        [Fact]
        public void TryCreateSafeSourceServerCommand_NullCommand_ReturnsFalse()
        {
            Assert.False(TryCreateSafeSourceServerCommand(null, TfExe, OutputPath, out string safe, out string reason));
            Assert.Null(safe);
            Assert.NotNull(reason);
        }

        [Fact]
        public void TryCreateSafeSourceServerCommand_EmptyCommand_ReturnsFalse()
        {
            Assert.False(TryCreateSafeSourceServerCommand("   ", TfExe, OutputPath, out string safe, out string reason));
            Assert.Null(safe);
            Assert.NotNull(reason);
        }

        [Fact]
        public void TryCreateSafeSourceServerCommand_NullTfExe_ReturnsFalse()
        {
            Assert.False(TryCreateSafeSourceServerCommand("tf.exe view", null, OutputPath, out string safe, out string reason));
            Assert.Null(safe);
        }

        [Fact]
        public void TryCreateSafeSourceServerCommand_NullOutputPath_ReturnsFalse()
        {
            Assert.False(TryCreateSafeSourceServerCommand("tf.exe view", TfExe, null, out string safe, out string reason));
            Assert.Null(safe);
        }

        // --- Modern, real-world legitimate command shapes -----------------------------------------------

        [Fact]
        public void TryCreateSafeSourceServerCommand_TfvcModernTemplate_Accepted()
        {
            // This is the literal TFS_EXTRACT_CMD shape from clr.pdb after %var3% / %var4% substitution.
            string command = "tf.exe view /version:12345 /noprompt \"$/team/path/Program.cs\" " +
                "/server:https://tfs.example.com/tfs/DefaultCollection /output:c:\\original\\path.cs";

            Assert.True(TryCreateSafeSourceServerCommand(command, TfExe, OutputPath, out string safe, out string reason));
            Assert.Null(reason);

            // Tool prefix is preserved and quoted because TfExe has no spaces here.
            Assert.StartsWith(TfExe, safe);

            Assert.Contains(" view ", safe);
            Assert.Contains("/version:12345", safe);
            Assert.Contains("/noprompt", safe);
            Assert.Contains("$/team/path/Program.cs", safe);
            Assert.Contains("/server:https://tfs.example.com/tfs/DefaultCollection", safe);

            // PDB-supplied /output was stripped; our own /output:<safeCachePath> is appended.
            Assert.DoesNotContain("c:\\original\\path.cs", safe);
            Assert.EndsWith("/output:" + OutputPath, safe);
        }

        [Fact]
        public void TryCreateSafeSourceServerCommand_TfvcLegacyConsoleFlag_Accepted()
        {
            // Older TFVC templates carried /console; we accept it as a no-op because we always force
            // /output:.  This keeps the safe rewriter from rejecting legitimate legacy PDBs.
            string command = "tf.exe view /version:42 /noprompt /console \"$/foo/bar.cs\" /server:https://tfs.example.com";

            Assert.True(TryCreateSafeSourceServerCommand(command, TfExe, OutputPath, out string safe, out string reason));
            Assert.Null(reason);
            Assert.Contains("/console", safe);
            Assert.EndsWith("/output:" + OutputPath, safe);
        }

        [Fact]
        public void TryCreateSafeSourceServerCommand_TfvcNamespaceView_Accepted()
        {
            // tf.exe also supports the explicit 'vc' namespace for version-control commands.  Treat
            // 'tf.exe vc view' as the same TFVC shape as 'tf.exe view' and apply the same allow-list.
            string command = "tf.exe vc view /version:12345 /noprompt \"$/team/path/Program.cs\" " +
                "/server:https://tfs.example.com/tfs/DefaultCollection /output:c:\\original\\path.cs";

            Assert.True(TryCreateSafeSourceServerCommand(command, TfExe, OutputPath, out string safe, out string reason));
            Assert.Null(reason);

            Assert.Contains(" vc view ", safe);
            Assert.Contains("/version:12345", safe);
            Assert.Contains("/noprompt", safe);
            Assert.Contains("$/team/path/Program.cs", safe);
            Assert.Contains("/server:https://tfs.example.com/tfs/DefaultCollection", safe);

            // PDB-supplied /output was stripped; our own /output:<safeCachePath> is appended.
            Assert.DoesNotContain("c:\\original\\path.cs", safe);
            Assert.EndsWith("/output:" + OutputPath, safe);
        }

        [Fact]
        public void TryCreateSafeSourceServerCommand_TfGitViewModernTemplate_Accepted()
        {
            // The literal shape from modern Windows PDBs (ntdll.pdb / ole32.pdb / fltMgr.pdb).
            string command = "tf.exe git view " +
                "/collection:https://dev.azure.com/microsoft " +
                "/teamproject:os " +
                "/repository:os.2020 " +
                "/commitid:0123456789abcdef0123456789abcdef01234567 " +
                "/path:src/foo/bar.cpp " +
                "/output:c:\\anywhere.cpp " +
                "/applyfilters";

            Assert.True(TryCreateSafeSourceServerCommand(command, TfExe, OutputPath, out string safe, out string reason));
            Assert.Null(reason);

            Assert.Contains(" git view ", safe);
            Assert.Contains("/collection:https://dev.azure.com/microsoft", safe);
            Assert.Contains("/teamproject:os", safe);
            Assert.Contains("/repository:os.2020", safe);
            Assert.Contains("/commitid:0123456789abcdef0123456789abcdef01234567", safe);
            Assert.Contains("/path:src/foo/bar.cpp", safe);
            Assert.Contains("/applyfilters", safe);

            // PDB-supplied output was stripped; our /output: is appended at the end.
            Assert.DoesNotContain("c:\\anywhere.cpp", safe);
            Assert.EndsWith("/output:" + OutputPath, safe);
        }

        [Fact]
        public void TryCreateSafeSourceServerCommand_TfvcWithDashSwitches_Accepted()
        {
            // tf.exe accepts both '/' and '-' switch prefixes; the allow-list and stripping logic must
            // handle both equally.  This also exercises the '=' value separator on /version.
            string command = "tf.exe view -version=99 -noprompt -server=https://tfs.example.com \"$/x/y.cs\"";

            Assert.True(TryCreateSafeSourceServerCommand(command, TfExe, OutputPath, out string safe, out string reason));
            Assert.Null(reason);
            Assert.Contains("-version=99", safe);
            Assert.Contains("-noprompt", safe);
        }

        // --- Output-stripping invariant -----------------------------------------------------------------

        [Fact]
        public void TryCreateSafeSourceServerCommand_PdbOutputAttachedColon_IsStripped()
        {
            string command = "tf.exe view /version:1 /noprompt /server:https://t /output:C:\\evil.txt \"$/a.cs\"";

            Assert.True(TryCreateSafeSourceServerCommand(command, TfExe, OutputPath, out string safe, out string reason));
            Assert.DoesNotContain("C:\\evil.txt", safe);
            Assert.EndsWith("/output:" + OutputPath, safe);
        }

        [Fact]
        public void TryCreateSafeSourceServerCommand_PdbOutputAttachedEquals_IsStripped()
        {
            string command = "tf.exe view /version:1 /noprompt /server:https://t /output=C:\\evil.txt \"$/a.cs\"";

            Assert.True(TryCreateSafeSourceServerCommand(command, TfExe, OutputPath, out string safe, out string reason));
            Assert.DoesNotContain("C:\\evil.txt", safe);
            Assert.EndsWith("/output:" + OutputPath, safe);
        }

        [Fact]
        public void TryCreateSafeSourceServerCommand_PdbOutputSpaceSeparated_IsStripped()
        {
            // /output as a flag followed by the value as a separate token.  The rewriter must consume both.
            string command = "tf.exe view /version:1 /noprompt /server:https://t /output C:\\evil.txt \"$/a.cs\"";

            Assert.True(TryCreateSafeSourceServerCommand(command, TfExe, OutputPath, out string safe, out string reason));
            Assert.DoesNotContain("C:\\evil.txt", safe);
            Assert.EndsWith("/output:" + OutputPath, safe);
        }

        [Fact]
        public void TryCreateSafeSourceServerCommand_PdbOutputDashPrefix_IsStripped()
        {
            string command = "tf.exe view /version:1 /noprompt /server:https://t -output:C:\\evil.txt \"$/a.cs\"";

            Assert.True(TryCreateSafeSourceServerCommand(command, TfExe, OutputPath, out string safe, out string reason));
            Assert.DoesNotContain("C:\\evil.txt", safe);
            Assert.EndsWith("/output:" + OutputPath, safe);
        }

        [Fact]
        public void TryCreateSafeSourceServerCommand_PdbLogin_IsStripped()
        {
            // PDB-supplied credentials are nonsense and dangerous; they must be dropped.
            string command = "tf.exe view /version:1 /noprompt /server:https://t /login:user,secret \"$/a.cs\"";

            Assert.True(TryCreateSafeSourceServerCommand(command, TfExe, OutputPath, out string safe, out string reason));
            Assert.DoesNotContain("/login", safe);
            Assert.DoesNotContain("secret", safe);
        }

        [Fact]
        public void TryCreateSafeSourceServerCommand_BareOutputDoesNotEatFollowingSwitch()
        {
            // If a malformed PDB emits '/output' with no value before another allow-listed switch, the
            // rewriter must not silently consume that switch as the (missing) value.  Otherwise the safely-
            // rebuilt command shown to the user in the consent prompt would silently omit /server:...,
            // /version:..., or any other legitimate switch that happened to follow.
            string command = "tf.exe view /noprompt /output /server:https://tfs.example.com /version:42 \"$/a.cs\"";

            Assert.True(TryCreateSafeSourceServerCommand(command, TfExe, OutputPath, out string safe, out string reason));
            Assert.Contains("/server:https://tfs.example.com", safe);
            Assert.Contains("/version:42", safe);
            Assert.EndsWith("/output:" + OutputPath, safe);
        }

        [Fact]
        public void TryCreateSafeSourceServerCommand_BareLoginDoesNotEatFollowingSwitch()
        {
            // Same protection as for bare /output: a /login with no value must not silently consume the
            // next allow-listed switch.
            string command = "tf.exe view /noprompt /login /server:https://tfs.example.com /version:42 \"$/a.cs\"";

            Assert.True(TryCreateSafeSourceServerCommand(command, TfExe, OutputPath, out string safe, out string reason));
            Assert.Contains("/server:https://tfs.example.com", safe);
            Assert.Contains("/version:42", safe);
            Assert.DoesNotContain("/login", safe);
        }

        [Fact]
        public void TryCreateSafeSourceServerCommand_BareOutputBeforeRedirection_DoesNotEatRedirection()
        {
            // A bare /output followed by '>file' must not consume the redirection token; the main walk's
            // '>' handling should still terminate parsing at the redirection.
            string command = "tf.exe view /version:1 /noprompt /server:https://t \"$/a.cs\" /output >C:\\evil.txt";

            Assert.True(TryCreateSafeSourceServerCommand(command, TfExe, OutputPath, out string safe, out string reason));
            Assert.DoesNotContain("C:\\evil.txt", safe);
            Assert.DoesNotContain(">", safe);
        }

        [Fact]
        public void TryCreateSafeSourceServerCommand_LegacyShellRedirection_StopsParsing()
        {
            // Legacy SRCSRV templates that ran through cmd /c sometimes used '>file' redirection.  Anything
            // after a '>'-prefixed token is dropped (we always write via /output:).
            string command = "tf.exe view /version:1 /noprompt /server:https://t \"$/a.cs\" >C:\\evil.txt";

            Assert.True(TryCreateSafeSourceServerCommand(command, TfExe, OutputPath, out string safe, out string reason));
            Assert.DoesNotContain("C:\\evil.txt", safe);
            Assert.DoesNotContain(">", safe);
        }

        [Fact]
        public void TryCreateSafeSourceServerCommand_StdoutRedirectToStderr_Rejected()
        {
            // We do not run through cmd /c, so shell redirection is not interpreted.  Still, a redirection
            // token like 1>&2 is not part of any supported source-server shape and should be rejected rather
            // than copied into the rebuilt command line.
            string command = "tf.exe view /version:1 /noprompt /server:https://t \"$/a.cs\" 1>&2";

            Assert.False(TryCreateSafeSourceServerCommand(command, TfExe, OutputPath, out string safe, out string reason));
            Assert.Null(safe);
            Assert.NotNull(reason);
            Assert.Contains("1>&2", reason);
        }

        // --- Rejection paths ----------------------------------------------------------------------------

        [Fact]
        public void TryCreateSafeSourceServerCommand_CmdExe_Rejected()
        {
            string command = "cmd.exe /c echo pwned > C:\\evil.txt";

            Assert.False(TryCreateSafeSourceServerCommand(command, TfExe, OutputPath, out string safe, out string reason));
            Assert.Null(safe);
            Assert.NotNull(reason);
        }

        [Fact]
        public void TryCreateSafeSourceServerCommand_FastVstsBlob_Rejected()
        {
            // fastVstsBlob.exe is observed in real PDBs but we intentionally don't support it (the same
            // PDBs declare tf.exe git view as a fallback).
            string command = "fastVstsBlob.exe /BlobStore:https://x /blobid:abc /output:C:\\out.cs";

            Assert.False(TryCreateSafeSourceServerCommand(command, TfExe, OutputPath, out string safe, out string reason));
            Assert.Null(safe);
            Assert.NotNull(reason);
        }

        [Fact]
        public void TryCreateSafeSourceServerCommand_SdExePerforce_Rejected()
        {
            string command = "sd.exe -p server:port print -o C:\\out.cs -q //depot/file.cs";

            Assert.False(TryCreateSafeSourceServerCommand(command, TfExe, OutputPath, out string safe, out string reason));
            Assert.Null(safe);
            Assert.NotNull(reason);
        }

        [Fact]
        public void TryCreateSafeSourceServerCommand_TfvcWorkfold_Rejected()
        {
            // 'tf.exe workfold' is a real tf.exe subcommand but not 'view' / 'git view', so it must be
            // rejected.  Otherwise an attacker could pivot the allow-list across tf.exe subcommands.
            string command = "tf.exe workfold /map C:\\evil";

            Assert.False(TryCreateSafeSourceServerCommand(command, TfExe, OutputPath, out string safe, out string reason));
            Assert.Null(safe);
            Assert.NotNull(reason);
        }

        [Fact]
        public void TryCreateSafeSourceServerCommand_UnknownSwitchOnTfvc_Rejected()
        {
            string command = "tf.exe view /version:1 /noprompt /shelve:foo \"$/a.cs\"";

            Assert.False(TryCreateSafeSourceServerCommand(command, TfExe, OutputPath, out string safe, out string reason));
            Assert.Null(safe);
            Assert.NotNull(reason);
            Assert.Contains("/shelve", reason);
        }

        [Fact]
        public void TryCreateSafeSourceServerCommand_UnknownSwitchOnTfvcNamespaceView_Rejected()
        {
            string command = "tf.exe vc view /version:1 /noprompt /shelve:foo \"$/a.cs\"";

            Assert.False(TryCreateSafeSourceServerCommand(command, TfExe, OutputPath, out string safe, out string reason));
            Assert.Null(safe);
            Assert.NotNull(reason);
            Assert.Contains("/shelve", reason);
        }

        [Fact]
        public void TryCreateSafeSourceServerCommand_UnknownSwitchOnGitView_Rejected()
        {
            string command = "tf.exe git view /collection:https://x /teamproject:p /repository:r /commitid:c /path:f /evil:bad";

            Assert.False(TryCreateSafeSourceServerCommand(command, TfExe, OutputPath, out string safe, out string reason));
            Assert.Null(safe);
            Assert.NotNull(reason);
            Assert.Contains("/evil", reason);
        }

        [Fact]
        public void TryCreateSafeSourceServerCommand_TfvcExtraPositional_Rejected()
        {
            // Only one positional ($-prefixed) argument is allowed for tf.exe view.  A second positional
            // is a sign that the PDB is up to something unexpected, so reject it.
            string command = "tf.exe view /version:1 /noprompt /server:https://t \"$/a.cs\" \"$/b.cs\"";

            Assert.False(TryCreateSafeSourceServerCommand(command, TfExe, OutputPath, out string safe, out string reason));
            Assert.Null(safe);
        }

        [Fact]
        public void TryCreateSafeSourceServerCommand_TfvcNonDollarPositional_Rejected()
        {
            string command = "tf.exe view /version:1 /noprompt /server:https://t junk.cs";

            Assert.False(TryCreateSafeSourceServerCommand(command, TfExe, OutputPath, out string safe, out string reason));
            Assert.Null(safe);
        }

        [Theory]
        [InlineData("$")]
        [InlineData("$/")]
        public void TryCreateSafeSourceServerCommand_TfvcTooShortDepotPath_Rejected(string depotPath)
        {
            string command = "tf.exe view /version:1 /noprompt /server:https://t \"" + depotPath + "\"";

            Assert.False(TryCreateSafeSourceServerCommand(command, TfExe, OutputPath, out string safe, out string reason));
            Assert.Null(safe);
            Assert.NotNull(reason);
            Assert.Contains(depotPath, reason);
        }

        [Fact]
        public void TryCreateSafeSourceServerCommand_GitViewPositional_Rejected()
        {
            // tf.exe git view templates never carry positional arguments.  Any positional means the PDB
            // has gone off-script, so reject.
            string command = "tf.exe git view /collection:https://x /teamproject:p /repository:r /commitid:c /path:f extraToken";

            Assert.False(TryCreateSafeSourceServerCommand(command, TfExe, OutputPath, out string safe, out string reason));
            Assert.Null(safe);
        }

        [Fact]
        public void TryCreateSafeSourceServerCommand_UnterminatedQuote_Rejected()
        {
            string command = "tf.exe view /version:1 /noprompt /server:https://t \"$/a.cs";

            Assert.False(TryCreateSafeSourceServerCommand(command, TfExe, OutputPath, out string safe, out string reason));
            Assert.Null(safe);
            Assert.NotNull(reason);
        }

        [Fact]
        public void TryCreateSafeSourceServerCommand_ShellMetacharactersInSwitchValue_DoNotEscape()
        {
            // Even if a malicious PDB jams shell metacharacters into a /server: value, we no longer go
            // through cmd /c, so the rebuilt command does not interpret them.  The token survives only
            // as a single literal argument value to tf.exe (which will then fail in its own URL parser).
            string command = "tf.exe view /version:1 /noprompt \"$/a.cs\" /server:https://t&calc.exe";

            Assert.True(TryCreateSafeSourceServerCommand(command, TfExe, OutputPath, out string safe, out string reason));
            Assert.Null(reason);
            // The metacharacters are preserved as part of a single argument value -- not as separate
            // shell tokens.  Quoting may or may not be applied depending on whitespace.
            Assert.Contains("https://t&calc.exe", safe);
        }

        // --- Quoting ------------------------------------------------------------------------------------

        [Fact]
        public void TryCreateSafeSourceServerCommand_TfExePathWithSpaces_IsQuoted()
        {
            string tfExeWithSpaces = @"C:\Program Files\Common\tf.exe";
            string command = "tf.exe view /version:1 /noprompt /server:https://t \"$/a.cs\"";

            Assert.True(TryCreateSafeSourceServerCommand(command, tfExeWithSpaces, OutputPath, out string safe, out string reason));
            Assert.StartsWith("\"C:\\Program Files\\Common\\tf.exe\"", safe);
        }

        [Fact]
        public void TryCreateSafeSourceServerCommand_OutputPathWithSpaces_IsQuoted()
        {
            string outputWithSpaces = @"C:\My Cache\abc\file.cs";
            string command = "tf.exe view /version:1 /noprompt /server:https://t \"$/a.cs\"";

            Assert.True(TryCreateSafeSourceServerCommand(command, TfExe, outputWithSpaces, out string safe, out string reason));
            Assert.EndsWith("\"/output:" + outputWithSpaces + "\"", safe);
        }
    }
}
