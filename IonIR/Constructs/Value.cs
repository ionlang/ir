using System;
using Ion.IR.Cognition;
using Ion.IR.Target;

namespace Ion.IR.Constructs
{
    public class Value : Construct
    {
        public override ConstructType ConstructType => ConstructType.Value;

        public Kind Kind { get; }

        public string Content { get; }

        public Value(Kind kind, string content)
        {
            this.Kind = kind;
            this.Content = content;
        }

        public override string Emit()
        {
            // TODO: Hard-coded symbols.
            return $"({this.Kind.Emit()}){this.Content}";
        }

        public LlvmValue AsLlvmValue()
        {
            // Ensure value is identified as a literal.
            if (!Recognition.IsLiteral(this.Content))
            {
                throw new Exception("Content could not be identified as a valid literal");
            }
            // Integer literal.
            else if (Recognition.IsInteger(this.Content))
            {
                return LlvmConstFactory.Int(this.Kind.AsLlvmType(), int.Parse(this.Content));
            }
            // String literal.
            else if (Recognition.IsStringLiteral(this.Content))
            {
                return LlvmConstFactory.String(this.Content);
            }
            // Unrecognized literal.
            else {
                throw new Exception($"Unrecognized literal: {this.Content}");
            }
        }
    }
}
