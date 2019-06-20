using Ion.Engine.Llvm;
using Ion.IR.Constants;
using Ion.IR.Constructs;

namespace Ion.IR.Instructions
{
    public class CallInstruction : Instruction
    {
        public LlvmFunction Target { get; }

        public string ResultIdentifier { get; }

        public Value[] Arguments { get; }

        // TODO: What about base inputs, ResultIdentifier?
        public CallInstruction(LlvmFunction target, string resultIdentifier, Value[] arguments) : base(InstructionName.Call, arguments)
        {
            this.Target = target;
            this.ResultIdentifier = resultIdentifier;
            this.Arguments = arguments;
        }
    }
}
