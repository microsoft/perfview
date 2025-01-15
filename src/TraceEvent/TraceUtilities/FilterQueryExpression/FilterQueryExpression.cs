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
        internal enum Operator
        {
            NotValid,
            Equal,
            NotEqualTo,
            GreaterThan,
            GreaterThanOrEqualTo,
            LessThan,
            LessThanOrEqualTo,
            Contains
        };

        private static readonly string[] _separator      = new[] { " " };
        private static readonly string[] _eventSeparator = new[] { "::" };

        private static readonly Dictionary<string, Operator> _operatorMap =
            new Dictionary<string, Operator>(StringComparer.OrdinalIgnoreCase)
            {
                { "==" , Operator.Equal },
                { "!=" , Operator.NotEqualTo },
                { ">", Operator.GreaterThan },
                { ">=", Operator.GreaterThanOrEqualTo },
                { "<", Operator.LessThan },
                { "<=", Operator.LessThanOrEqualTo },
                { "contains", Operator.Contains },
            };

        private static HashSet<string> _uniqueNumericOperators 
            = new HashSet<string> { "!=", "<=", ">=", "==", "<", ">" };

        private readonly string _expression;
        private readonly string[] _lhsSplit;

        public FilterQueryExpression(string expression)
        {
            if (!expression.Contains("contains") && !expression.Contains("Contains"))
            {
                expression = expression.Replace(" ", "");
                foreach(var o in _uniqueNumericOperators)
                {
                    if (expression.Contains(o))
                    {
                        var replaced = expression.Split(new string[] { o }, StringSplitOptions.RemoveEmptyEntries);
                        LeftOperand  = replaced[0];
                        RightOperand = replaced[1];
                        Op           = _operatorMap[o];
                        break;
                    }
                }

                // If none of the supplied operators match, we know something is wrong.
                if (Op == Operator.NotValid || string.IsNullOrWhiteSpace(LeftOperand) || string.IsNullOrWhiteSpace(RightOperand))
                {
                    throw new FilterQueryExpressionTreeParsingException("Invalid Expression.", expression);
                }
            }

            // The `contains` must be separated by spaces.
            else
            {
                var splits = expression.Split(_separator, StringSplitOptions.RemoveEmptyEntries); 
                LeftOperand = splits[0];
                string operatorAsString = splits[1];

                if (_operatorMap.TryGetValue(operatorAsString, out Operator op))
                {
                    Op = op;
                }
                else
                {
                    throw new FilterQueryExpressionParsingException($"Operator: {operatorAsString} is not a valid operator", expression);
                }

                RightOperand = splits[2];
            }

            IsDouble = double.TryParse(RightOperand, out var rhsAsDouble);
            if (IsDouble)
            {
                RightOperandAsDouble = rhsAsDouble;
            }

            _lhsSplit = LeftOperand.Split(_eventSeparator, StringSplitOptions.RemoveEmptyEntries);
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

            if (_lhsSplit.Length == 1)
            {
                rhsOperand = FilterQueryUtilities.ExtractPayloadByName(@event, LeftOperand);
            }

            else
            {
                var eventName = @event.EventName;

                // If the event name is provided, try to match on it.
                if (!eventName.Contains(_lhsSplit[0]))
                {
                    return false;
                }

                rhsOperand = FilterQueryUtilities.ExtractPayloadByName(@event, _lhsSplit[1]);
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

            if (_lhsSplit.Length == 1)
            {
                // If the property names.
                if (!propertyNamesToValues.TryGetValue(_lhsSplit[0], out rhsOperand))
                {
                    return false;
                }
            }

            else
            {
                // If the event name is provided, try to match on it.
                if (!eventName.Contains(_lhsSplit[0]))
                {
                    return false;
                }

                if (!propertyNamesToValues.TryGetValue(_lhsSplit[1], out rhsOperand))
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

        public string LeftOperand { get; private set; } = string.Empty;
        public Operator Op { get; private set; }
        public string RightOperand { get; private set; }
        public double RightOperandAsDouble { get; private set; } = double.NaN;
        public bool IsDouble { get; private set; }

        public override string ToString()
            => $"{LeftOperand} {Op} {RightOperand}";
    }
}
