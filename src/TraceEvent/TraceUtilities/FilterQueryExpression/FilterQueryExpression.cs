using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Tracing.TraceUtilities.FilterQueryExpression
{
    /// <summary>
    /// This class encapsulates a single expression of the type ``LeftOperand Operator RightOperand``.
    /// - ``LeftOperand`` represents either the name of a property or an event name and a property. The format here is: ``PropertyName`` or ``EventName::PropertyName``.
    /// - Acceptable ``Operators`` include &lt; &lt;=, &gt;, &gt;=, !=, = and Contains.
    /// - ``RightOperand`` must be either a string or a double. 
    /// </summary>
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

        private readonly string _expression;

        public FilterQueryExpression(string expression)
        {
            _expression = expression;
            var splits = expression.Split(_separator, StringSplitOptions.RemoveEmptyEntries); 
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
            if (split.Length != 3)
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

        /// <summary>
        /// Method responsible for checking if a trace event's contents match the expression. If so, return true, otherwise, return false.
        /// </summary>
        /// <param name="event"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
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
                    // Should never get here.
                    throw new FilterQueryExpressionParsingException($"Unidentified Operator: {Op}", _expression);
            }
        }

        public bool Match(Dictionary<string, string> propertyNamesToValues, string eventName)
        {
            string rhsOperand = null;
            string[] lhsSplit = LeftOperand.Split(_eventSeparator, StringSplitOptions.RemoveEmptyEntries);

            if (lhsSplit.Length == 1)
            {
                // If the property names.
                if (!propertyNamesToValues.TryGetValue(lhsSplit[0], out rhsOperand))
                {
                    return false;
                }
            }

            else
            {
                // If the event name is provided, try to match on it.
                if (!eventName.Contains(lhsSplit[0]))
                {
                    return false;
                }

                if (!propertyNamesToValues.TryGetValue(lhsSplit[1], out rhsOperand))
                {
                    return false;
                }
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
                    // Should never get here.
                    throw new FilterQueryExpressionParsingException($"Unidentified Operator: {Op}", _expression);
            }
        }

        /// <summary>
        /// Helper method responsible for handling the case when the rhsOperand is a double.
        /// </summary>
        /// <param name="expression"></param>
        /// <param name="rhsOperand"></param>
        /// <param name="o"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        internal static bool HandleDoubleComparisons(FilterQueryExpression expression, string rhsOperand, Operator o)
        {
            if (!expression.IsDouble)
            {
                throw new FilterQueryExpressionParsingException($"Right Operand: {rhsOperand} is not a double.", expression._expression);
            }

            if (!double.TryParse(rhsOperand, out double rhsOperandAsDouble))
            {
                throw new FilterQueryExpressionParsingException($"Right Operand: {rhsOperand} is not a double.", expression._expression);
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
                    throw new FilterQueryExpressionParsingException($"Unidentified Operator: {o}.", expression._expression);
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
