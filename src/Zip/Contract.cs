using System;

namespace System.Diagnostics.Contracts2
{
    static class Contract
    {
        // Methods
        [Conditional("DEBUG")]
        [Conditional("CONTRACTS_FULL")]
        public static void Assert(bool condition) {}

        [Conditional("DEBUG")]
        [Conditional("CONTRACTS_FULL")]
        public static void Assert(bool condition, string userMessage) {}

        [Conditional("DEBUG")]
        [Conditional("CONTRACTS_FULL")]
        public static void Assume(bool condition) { }

        [Conditional("DEBUG")]
        [Conditional("CONTRACTS_FULL")]
        public static void Assume(bool condition, string userMessage) {}

        [Conditional("CONTRACTS_FULL")]
        public static void EndContractBlock() {}

        [Conditional("CONTRACTS_FULL")]
        public static void Ensures(bool condition) {}

        [Conditional("CONTRACTS_FULL")]
        public static void Ensures(bool condition, string userMessage) {}
#if false 
        [Conditional("CONTRACTS_FULL")]
        public static void EnsuresOnThrow<TException>(bool condition) where TException : Exception {}

        [Conditional("CONTRACTS_FULL")]
        public static void EnsuresOnThrow<TException>(bool condition, string userMessage) where TException : Exception {}

        public static bool Exists<T>(IEnumerable<T> collection, Predicate<T> predicate){}
        
        public static bool Exists(int fromInclusive, int toExclusive, Predicate<int> predicate) {}
        
        public static bool ForAll<T>(IEnumerable<T> collection, Predicate<T> predicate) {}
        
        public static bool ForAll(int fromInclusive, int toExclusive, Predicate<int> predicate) {}

        [Conditional("CONTRACTS_FULL")]
        public static void Invariant(bool condition) {}

        [Conditional("CONTRACTS_FULL")]
        public static void Invariant(bool condition, string userMessage) {}
        public static T OldValue<T>(T value) {}
        private static void ReportFailure(ContractFailureKind failureKind, string userMessage, string conditionText, Exception innerException);

        [Conditional("CONTRACTS_FULL")]
        public static void Requires(bool condition);
        
        public static void Requires<TException>(bool condition) where TException : Exception;
        public static void Requires(bool condition, string userMessage);
        public static void Requires<TException>(bool condition, string userMessage) where TException : Exception;
#endif
        public static T Result<T>() { return default(T); }
        public static T ValueAtReturn<T>(out T value) { value = default(T); return value;  } 

	}
}
