using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Tracing.TraceUtilities.FilterQueryExpression
{
    /// <summary>
    /// Class responsible for encapsulating the collection of FilterQueryExpressions and the logic to combine them to deduce if we match on a particular trace event.
    /// </summary>
    public sealed class FilterQueryExpressionTree
    {
        private readonly FilterQueryExpression _simpleFilterQueryExpression;
        private readonly Dictionary<char, FilterQueryExpression> _expressionMap;
        private readonly string _postFixExpression;

        public FilterQueryExpressionTree(string expression)
        {
            if (string.IsNullOrEmpty(expression))
            {
                throw new FilterQueryExpressionTreeParsingException($"{nameof(expression)} is null.", expression); 
            }

            // Faster computation if a single Expression Without Parentheses i.e. one simple expression is provided.
            if (FilterQueryExpression.IsValidExpression(expression) &&
                (!expression.Contains("(") || expression.Contains(")")))
            {
                _simpleFilterQueryExpression = new FilterQueryExpression(expression);
            }

            // Compute the PostFix (RPN) representation of the expression for easier logical deduction where perf matters.
            else
            {
                OriginalExpression = expression;
                string primedExpression = ShuntingYard.PrimeExpression(OriginalExpression, out var expressionMap);
                _expressionMap = expressionMap; 
                _postFixExpression = ShuntingYard.ToPostFix(primedExpression);
            }
        }

        /// <summary>
        /// Keep track of the original expression the user provided.
        /// </summary>
        public string OriginalExpression { get; }

        /// <summary>
        /// This method is responsible for matching the entire user specified expression on an event.  
        /// </summary>
        /// <param name="event"></param>
        /// <returns></returns>
        public bool Match(TraceEvent @event)
        {
            // Handle the simple case where a single simple filter query expression is provided.
            if (_simpleFilterQueryExpression != null)
            {
                return _simpleFilterQueryExpression.Match(@event);
            }

            // Map each of the expressions to lower-case alphabet and the match result of the expression i.e. a boolean.
            Dictionary<string, bool> convertedExpressionMap = new Dictionary<string, bool>();
            foreach (var kvp in _expressionMap)
            {
                convertedExpressionMap[kvp.Key.ToString()] = kvp.Value.Match(@event);
            }

            // Conduct a Shunting Yard Match using the modified postfix expression.
            return ShuntingYard.Match(_postFixExpression, convertedExpressionMap);
        }
    }
}
