using System;

namespace Microsoft.Diagnostics.Tracing.TraceUtilities.FilterQueryExpression
{
    internal sealed class FilterQueryExpression
    {
        private static readonly string[] _separator      = new[] { " " };
        private static readonly string[] _eventSeparator = new[] { "::" };

        public enum Operator
        {
            NotValid,
            Equal,
            GreaterThan,
            GreaterThanOrEqualTo,
            LessThan,
            LessThanOrEqualTo,
            NotEqualTo,
            Contains
        };

        public FilterQueryExpression(string exp)
        {
            var splits = exp.Split(_separator, StringSplitOptions.RemoveEmptyEntries); 
            LeftOperand = splits[0];
            OperatorAsString = splits[1].ToLower();
            switch (OperatorAsString)
            {
                case "=":
                    Op = Operator.Equal;
                    break;
                case "!=":
                    Op = Operator.NotEqualTo;
                    break;
                case "contains":
                    Op = Operator.Contains;
                    break;
                case ">":
                    Op = Operator.GreaterThan;
                    break;
                case ">=":
                    Op = Operator.GreaterThanOrEqualTo;
                    break;
                case "<":
                    Op = Operator.LessThan;
                    break;
                case "<=":
                    Op = Operator.LessThanOrEqualTo;
                    break;
                default:
                    Op = Operator.NotValid;
                    break;
            }

            RightOperand = splits[2];
            IsDouble = double.TryParse(RightOperand, out var rhsAsDouble);
            if (IsDouble)
            {
                RightOperandAsDouble = rhsAsDouble;
            }
        }

        public static bool IsValidExpression(string expression)
        {
            var split = expression.Split(_separator, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 3)
                return false;

            var @operator = split[1].ToLower();
            return
                !split[0].Contains("(") &&
                !split[2].Contains(")") &&
                (@operator == "=" ||
                @operator == "!=" ||
                @operator == ">=" ||
                @operator == ">" ||
                @operator == "<=" ||
                @operator == "<" ||
                @operator == "contains");
        }

        public bool Match(TraceEvent @event)
        {
            string rhsOperand = null;
            string[] lhsSplit = LeftOperand.Split(_eventSeparator, StringSplitOptions.RemoveEmptyEntries);

            if (lhsSplit.Length == 1)
            {
                rhsOperand = FilterQueryUtilities.ExtractPayloadByName(@event, LeftOperand);
            }

            else
            {
                var eventName = @event.EventName;

                // If the event name is provided, try to match on it.
                if (!eventName.Contains(lhsSplit[0]))
                {
                    return false;
                }

                rhsOperand = FilterQueryUtilities.ExtractPayloadByName(@event, lhsSplit[1]);
            }

            // Payload not found => ignore this trace event.
            if (rhsOperand == null)
            {
                return false;
            }

            switch (Op)
            {
                case Operator.Equal:
                    return rhsOperand == RightOperand;
                case Operator.NotEqualTo:
                    return rhsOperand != RightOperand;
                case Operator.Contains:
                    return rhsOperand.Contains(RightOperand);

                case Operator.LessThan:
                case Operator.LessThanOrEqualTo:
                case Operator.GreaterThan:
                case Operator.GreaterThanOrEqualTo:
                    return HandleDoubleComparisons(this, rhsOperand, Op);
                default:
                    throw new ArgumentException("Unidentified Operator.");
            }
        }

        internal static bool HandleDoubleComparisons(FilterQueryExpression expression, string rhsOperand, Operator o)
        {
            if (!expression.IsDouble)
            {
                throw new ArgumentException($"Right Operand: {rhsOperand} is not a double.");
            }

            if (!double.TryParse(rhsOperand, out double rhsOperandAsDouble))
            {
                throw new ArgumentException($"Right Operand: {rhsOperand} is not a double.");
            }

            switch (o)
            {
                case Operator.LessThan:
                    return rhsOperandAsDouble < expression.RightOperandAsDouble;
                case Operator.LessThanOrEqualTo:
                    return rhsOperandAsDouble <= expression.RightOperandAsDouble;
                case Operator.GreaterThan:
                    return rhsOperandAsDouble > expression.RightOperandAsDouble;
                case Operator.GreaterThanOrEqualTo:
                    return rhsOperandAsDouble >= expression.RightOperandAsDouble;
                default:
                    throw new ArgumentException("Unidentified Operator.");
            }
        }

        public string LeftOperand { get; }
        public string OperatorAsString { get; }
        public Operator Op { get; }
        public string RightOperand { get; }
        public double RightOperandAsDouble { get; } = double.NaN;
        public bool IsDouble { get; }

        public override string ToString()
            => $"{LeftOperand} {OperatorAsString} {RightOperand}";
    }
}
