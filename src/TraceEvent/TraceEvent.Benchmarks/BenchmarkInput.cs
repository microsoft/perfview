using System;
using System.IO;

namespace TraceEventBenchmarks
{
    /// <summary>
    /// Loads benchmark input symbols from the inputs/ directory.
    /// </summary>
    internal static class BenchmarkInput
    {
        private static readonly string InputDir = Path.Combine(AppContext.BaseDirectory, "inputs");

        /// <summary>All C++ mangled symbols from cpp_symbols.txt.</summary>
        public static string[] CppSymbols { get; } = File.ReadAllLines(Path.Combine(InputDir, "cpp_symbols.txt"));

        /// <summary>C++ mangled symbols with ELF hash suffixes from cpp_symbols_metadata.txt.</summary>
        public static string[] CppSymbolsMetadata { get; } = File.ReadAllLines(Path.Combine(InputDir, "cpp_symbols_metadata.txt"));

        /// <summary>Sampled Rust v0 mangled symbols from rust_symbols.txt.</summary>
        public static string[] RustSymbols { get; } = File.ReadAllLines(Path.Combine(InputDir, "rust_symbols.txt"));

        // Representative symbols for micro-benchmarks (short / medium / long).

        public static readonly string CppShort = "_ZL5Usagev";
        public static readonly string CppMedium = "_Z12ataPrintMainP10ata_deviceRK17ata_print_options.cold";
        public static readonly string CppLong =
            "_ZNK5boost5proto6detail17reverse_fold_implINS0_6_stateENS1_18reverse_fold_tree_INS0_6tagns_3tag11shift_rightE" +
            "NS_6spirit6detail18make_binary_helperINS8_13meta_compilerINS8_2qi6domainEE12meta_grammarEEEEERKNS0_7exprns_4" +
            "exprIS7_NS0_7argsns_5list2IRKNSJ_IS7_NSL_IRKNSJ_IS7_NSL_IRKNSJ_IS7_NSL_IRKNSJ_IS7_NSL_IRKNSJ_IS7_NSL_IRKN" +
            "SJ_IS7_NSL_IRKNSJ_IS7_NSL_IRKNSJ_IS7_NSL_IRKNSJ_IS7_NSL_IRKNSJ_INS6_8terminalENSK_4termINS8_11terminal_ex" +
            "INS8_3tag3litENS_6fusion6vectorIJRA6_KcEEEEEEELl0EEENSJ_ISM_NSN_IRST_EELl0EEEEELl2EEERKNSJ_INS6_9subscript" +
            "ENSL_IRKNS8_8terminalINSP_7double_EEERKNS_7phoenix5actorINSI_10basic_exprINS6_6assignENSL_INS1G_INS1H_ISM_N" +
            "SN_INS_17reference_wrapperIdEEEELl0EEEEENS1G_INS8_8argumentILi0EEEEEEELl2EEEEEEELl2EEEEELl2EEERKNSJ_INS6_" +
            "6negateENSK_5list1IRKNSJ_ISM_NSN_INSO_ISQ_NSS_IJcEEEEEEELl0EEEEELl1EEEEELl2EEERKNSJ_IS19_NSL_IRKNS1A_INSP" +
            "_4int_EEERKNS1G_INS1H_IS1I_NSL_INS1G_INS1H_ISM_NSN_INS1J_IjEEEELl0EEEEES1Q_EELl2EEEEEEELl2EEEEELl2EEES2F" +
            "_EELl2EEERKNSJ_IS19_NSL_IS2N_RKNS1G_INS1H_IS1I_NSL_INS1G_INS1H_ISM_NSN_INS1J_IhEEEELl0EEEEES1Q_EELl2EEEE" +
            "EEELl2EEEEELl2EEES2F_EELl2EEES3L_EELl2EEES2F_EELl2EEERKNSJ_INS6_7modulusENSL_IRKNSJ_IS7_NSL_IRKNSJ_IS7_N" +
            "SL_IRKNSJ_IS7_NSL_IRKNSJ_IS7_NSL_IRKNSJ_IS19_NSL_IRKNSJ_IS7_NSL_IRKNSJ_IS7_NSL_IS1Z_S2F_EELl2EEES1Z_EELl" +
            "2EEERKNS1G_INS1H_INS1F_6detail3tag13function_evalENSK_5list3INS1H_ISM_NSN_INS1F_3stl9push_backEEELl0EEENS1G" +
            "_INS1H_ISM_NSN_INS1J_ISt6vectorISt4pairIddESaIS4L_EEEEEELl0EEEEENS1G_INS1H_ISM_NSN_INS1J_IKS4L_EEEELl0EEE" +
            "EEEELl3EEEEEEELl2EEES2F_EELl2EEERKNSJ_IS19_NSL_IS1E_RKNS1G_INS1H_IS4D_NS4E_IS4I_NS1G_INS1H_ISM_NSN_INS1J" +
            "_IS4J_IdSaIdEEEEEELl0EEEEES1Q_EELl3EEEEEEELl2EEEEELl2EEES2F_EELl2EEES5O_EELl2EEES2F_EELl2EEEEELl2EEENSR" +
            "_4consINSC_12literal_charINS8_13char_encoding8standardELb1ELb0EEENSR_4nil_EEERNS8_11unused_typeELl2EEclES68_R" +
            "KS6F_S6H_.isra.0";

        public static readonly string CppMetadataShort = "_Z7_assertbe77c5ed9e682170759be2ff8f760def48f5c89d3";
        public static readonly string CppMetadataMedium = "_ZN23ClarisDrawGraphInternal5Group11removeChildEib.cold085be0c262100b8f5f3ccf1a762bec0f0496f979";
        public static readonly string CppMetadataLong =
            "_ZNK5boost5proto6detail17reverse_fold_implINS0_6_stateENS1_18reverse_fold_tree_INS0_6tagns_3tag11shift_rightE" +
            "NS_6spirit6detail18make_binary_helperINS8_13meta_compilerINS8_2qi6domainEE12meta_grammarEEEEERKNS0_7exprns_4" +
            "exprIS7_NS0_7argsns_5list2IRKNSJ_IS7_NSL_IRKNSJ_IS7_NSL_IRKNSJ_IS7_NSL_IRKNSJ_IS7_NSL_IRKNSJ_IS7_NSL_IRKN" +
            "SJ_IS7_NSL_IRKNSJ_IS7_NSL_IRKNSJ_IS7_NSL_IRKNSJ_IS7_NSL_IRKNSJ_INS6_8terminalENSK_4termINS8_11terminal_ex" +
            "INS8_3tag3litENS_6fusion6vectorIJRA6_KcEEEEEEELl0EEENSJ_ISM_NSN_IRST_EELl0EEEEELl2EEERKNSJ_INS6_9subscript" +
            "ENSL_IRKNS8_8terminalINSP_7double_EEERKNS_7phoenix5actorINSI_10basic_exprINS6_6assignENSL_INS1G_INS1H_ISM_N" +
            "SN_INS_17reference_wrapperIdEEEELl0EEEEENS1G_INS8_8argumentILi0EEEEEEELl2EEEEEEELl2EEEEELl2EEERKNSJ_INS6_" +
            "6negateENSK_5list1IRKNSJ_ISM_NSN_INSO_ISQ_NSS_IJcEEEEEEELl0EEEEELl1EEEEELl2EEERKNSJ_IS19_NSL_IRKNS1A_INSP" +
            "_4int_EEERKNS1G_INS1H_IS1I_NSL_INS1G_INS1H_ISM_NSN_INS1J_IjEEEELl0EEEEES1Q_EELl2EEEEEEELl2EEEEELl2EEES2F" +
            "_EELl2EEERKNSJ_IS19_NSL_IS2N_RKNS1G_INS1H_IS1I_NSL_INS1G_INS1H_ISM_NSN_INS1J_IhEEEELl0EEEEES1Q_EELl2EEEE" +
            "EEELl2EEEEELl2EEES2F_EELl2EEES3L_EELl2EEES2F_EELl2EEERKNSJ_INS6_7modulusENSL_IRKNSJ_IS7_NSL_IRKNSJ_IS7_N" +
            "SL_IRKNSJ_IS7_NSL_IRKNSJ_IS7_NSL_IRKNSJ_IS19_NSL_IRKNSJ_IS7_NSL_IRKNSJ_IS7_NSL_IS1Z_S2F_EELl2EEES1Z_EELl" +
            "2EEERKNS1G_INS1H_INS1F_6detail3tag13function_evalENSK_5list3INS1H_ISM_NSN_INS1F_3stl9push_backEEELl0EEENS1G" +
            "_INS1H_ISM_NSN_INS1J_ISt6vectorISt4pairIddESaIS4L_EEEEEELl0EEEEENS1G_INS1H_ISM_NSN_INS1J_IKS4L_EEEELl0EEE" +
            "EEEELl3EEEEEEELl2EEES2F_EELl2EEERKNSJ_IS19_NSL_IS1E_RKNS1G_INS1H_IS4D_NS4E_IS4I_NS1G_INS1H_ISM_NSN_INS1J" +
            "_IS4J_IdSaIdEEEEEELl0EEEEES1Q_EELl3EEEEEEELl2EEEEELl2EEES2F_EELl2EEES5O_EELl2EEES2F_EELl2EEEEELl2EEENSR" +
            "_4consINSC_12literal_charINS8_13char_encoding8standardELb1ELb0EEENSR_4nil_EEERNS8_11unused_typeELl2EEclES68_R" +
            "KS6F_S6H_.isra.082144e53db49f2acb971a5fe3e96a45925f11afd";

        public static readonly string RustShort = "_RNvCsaL6DLWWPl8u_3log6logger";
        public static readonly string RustMedium =
            "_RNvXs1_NtCshgx866xcH9W_5alloc7raw_vecINtB5_6RawVecmENtNtNtCsiL7B6CFZHyD_4core3ops4drop4Drop4dropCsjqN8xXfUb5y_7bit_vec";
        public static readonly string RustLong =
            "_RNvXs2_NtCs9A9JS0ryDJM_3nom6branchTNCINvNtB7_8sequence10terminatedRShB12_INtNtCsiL7B6CFZHyD_4core6option6Op" +
            "tionhEINtNtB7_5error5ErrorB12_ENCINvNtB7_10combinator9recognizeB12_TINtNtCshgx866xcH9W_5alloc3vec3VechEhB2N_" +
            "EB1N_NCINvBC_5tupleB12_B2M_B1N_TNCINvNtB7_5multi5many1B12_hB1N_NCINvB2h_8completeB12_hB1N_NvNtCsfVvzz2Z4PLT" +
            "_5cexpr7literal7decimalE0E0NvNvB4V_7c_float6parserNCINvB42_5many0B12_hB1N_B4s_E0EE0E0NCINvB2h_3optB12_hB1N_N" +
            "vB4V_11float_widthE0E0NCIBA_B12_B12_B19_B1N_NCIB2f_B12_B2M_B1N_NCIB3z_B12_B2M_B1N_TB5Y_NvB5D_s_6parserB3X" +
            "_EE0E0B6x_E0NCIBA_B12_B12_B19_B1N_NCIB2f_B12_TB2N_B19_B2N_TB19_B2N_EEB1N_NCIB3z_B12_B9k_B1N_TB5Y_NCIB6A_B" +
            "12_hB1N_NvB5D_s0_6parserE0B3X_NvB4V_9float_expEE0E0B6x_E0NCIBA_B12_B12_B19_B1N_NCIB2f_B12_B9k_B1N_NCIB3z_" +
            "B12_B9k_B1N_TB3X_NCIB6A_B12_hB1N_NvB5D_s1_6parserE0B5Y_BaM_EE0E0B6x_E0NCIBA_B12_B12_hB1N_NCIB2f_B12_B2N_" +
            "B1N_B3X_E0B6T_E0EINtB5_3AltB12_B12_B1N_E6choiceB4X_";
    }
}
