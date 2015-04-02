using System;

namespace System.Diagnostics.Contracts
{
	class Contract
	{
        // Methods
        public static void Assert(bool condition) {}
        public static void Assert(bool condition, string userMessage) {}
        public static void Assume(bool condition) { }
        public static void Assume(bool condition, string userMessage) {}
        public static void EndContractBlock() {}
        public static void Ensures(bool condition) {}
        public static void Ensures(bool condition, string userMessage) {}
#if false 
        public static void EnsuresOnThrow<TException>(bool condition) where TException : Exception {}
        public static void EnsuresOnThrow<TException>(bool condition, string userMessage) where TException : Exception {}

        public static bool Exists<T>(IEnumerable<T> collection, Predicate<T> predicate){}
        public static bool Exists(int fromInclusive, int toExclusive, Predicate<int> predicate) {}
        public static bool ForAll<T>(IEnumerable<T> collection, Predicate<T> predicate) {}
        public static bool ForAll(int fromInclusive, int toExclusive, Predicate<int> predicate) {}
        public static void Invariant(bool condition) {}
        public static void Invariant(bool condition, string userMessage) {}
        public static T OldValue<T>(T value) {}
        private static void ReportFailure(ContractFailureKind failureKind, string userMessage, string conditionText, Exception innerException);
        public static void Requires(bool condition);
        public static void Requires<TException>(bool condition) where TException : Exception;
        public static void Requires(bool condition, string userMessage);
        public static void Requires<TException>(bool condition, string userMessage) where TException : Exception;
#endif
        public static T Result<T>() { return default(T); }
        public static T ValueAtReturn<T>(out T value) { value = default(T); return value;  } 

	}
}
