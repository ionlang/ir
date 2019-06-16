using Ion.IR.Constants;
using Ion.IR.Constructs;

namespace Ion.IR.Instructions
{
    public class CreateInstruction : Instruction
    {
        public string ResultIdentifier { get; }

        public Kind Kind { get; }

        public CreateInstruction(string resultIdentifier, Kind kind) : base(InstructionName.Create, new IConstruct[]
        {
            new Reference(resultIdentifier),
            kind
        })
        {
            this.ResultIdentifier = resultIdentifier;
            this.Kind = kind;
        }
    }
}
