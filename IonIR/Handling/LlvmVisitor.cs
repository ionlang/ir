#nullable enable

using System;
using System.Collections.Generic;
using Ion.Engine.Llvm;
using Ion.IR.Cognition;
using Ion.IR.Constants;
using Ion.IR.Constructs;
using Ion.IR.Tracking;
using Ion.IR.Visitor;
using LLVMSharp;

namespace Ion.IR.Handling
{
    public interface IVisitable<TNode, TVisitor>
    {
        TNode Accept(TVisitor visitor);
    }

    public partial class LlvmVisitor
    {
        public LlvmModule Module => this.module;

        protected readonly IrSymbolTable symbolTable;

        protected Stack<LlvmBlock> blockStack;

        protected Stack<LlvmValue> valueStack;

        protected Stack<LlvmType> typeStack;

        protected LlvmBuilder builder;

        protected LlvmModule module;

        protected LlvmFunction? function;

        protected Dictionary<string, LlvmValue> namedValues;

        public LlvmVisitor(LlvmModule module, LlvmBuilder builder)
        {
            this.module = module;
            this.builder = builder;
            this.symbolTable = new IrSymbolTable(this.module);
            this.valueStack = new Stack<LlvmValue>();
            this.typeStack = new Stack<LlvmType>();
            this.blockStack = new Stack<LlvmBlock>();
            this.namedValues = new Dictionary<string, LlvmValue>();
        }

        public LlvmVisitor(LlvmModule module) : this(module, LlvmBuilder.Create())
        {
            //
        }

        public Construct Visit(IVisitable<Constructs.Construct, LlvmVisitor> node)
        {
            // Ensure node is not null.
            if (node != null)
            {
                return node.Accept(this);
            }

            throw new ArgumentNullException("Node argument cannot be null");
        }

        public Construct VisitExtension(Construct node)
        {
            return node.VisitChildren(this);
        }

        public Construct VisitStruct(Struct node)
        {
            // Ensure target struct exists on the symbol table.
            if (!context.SymbolTable.structs.Contains(node.TargetIdentifier))
            {
                throw new Exception($"Reference to undefined struct named '${node.TargetIdentifier}'");
            }

            // Retrieve the symbol from the symbol table.
            StructSymbol symbol = context.SymbolTable.structs[this.TargetIdentifier];

            // Retrieve the target struct's LLVM reference value from the symbol.
            LLVMTypeRef structDef = symbol.Value;

            // Create a value buffer list.
            List<LLVMValueRef> values = new List<LLVMValueRef>();

            // Populate body properties.
            foreach (StructProperty property in node.Body)
            {
                // Emit and append the value to the buffer list.
                values.Add(property.Value.Emit(context));
            }

            // Create the resulting struct assignment value.
            LlvmValue assignment = LLVM.ConstNamedStruct(structDef, values.ToArray()).Wrap();

            // Append the assignment value onto the stack.
            this.valueStack.Push(assignment);

            // Return the node.
            return node;
        }

        public Construct VisitStructDefProperty(StructDefProperty node)
        {
            // Visit the kind.
            this.VisitKind(node.Kind);

            // TODO: Should register property along with its name on the symbol table somehow (name not being used).

            // Return the node.
            return node;
        }

        public Construct VisitStructDef(StructDef node)
        {
            // Create the body buffer list.
            List<LlvmType> body = new List<LlvmType>();

            // Create a buffer dictionary for the symbol.
            Dictionary<string, LlvmType> symbolProperties = new Dictionary<string, LlvmType>();

            // Map the body's properties onto the body.
            foreach (StructDefProperty property in node.Body)
            {
                // Visit the kind.
                this.VisitKind(property.Kind);

                // Pop the type off the stack.
                LlvmType type = this.typeStack.Pop();

                // Append it to the body.
                body.Add(type);

                // Append it to the symbol's properties dictionary.
                symbolProperties.Add(property.Identifier, type);
            }

            // Create the struct.
            LlvmType @struct = this.module.CreateStruct(node.Identifier, body.ToArray());

            // Append the resulting struct onto the stack.
            this.typeStack.Push(@struct);

            // Return the node.
            return node;
        }

        public Construct VisitVarDeclare(VarDeclare node)
        {
            // Create the variable.
            LlvmValue variable = LLVM.BuildAlloca(context.Target, this.ValueType.Emit(), node.Identifier);

            // Assign value if applicable.
            if (node.Value != null)
            {
                // Create the store instruction.
                LLVM.BuildStore(context.Target, node.Value.Emit(context), variable);

                // Register on symbol table.
                context.SymbolTable.localScope.Add(node.Identifier, variable);
            }

            // Append the value onto the stack.
            this.valueStack.Push(variable);

            // Return the node.
            return node;
        }

        public Construct VisitNumericExpr(NumericExpr node)
        {
            // Create the value.
            LlvmValue reference = Resolver.Literal(node.TokenType, node.Value, node.Type);

            // Append the value onto the stack.
            this.valueStack.Push(reference);

            // Return the node.
            return node;
        }

        public Construct VisitString(IR.Constructs.String node)
        {
            // TODO: Global string name.
            // Retrieve a string name.
            string name = "str";

            // Create the global string pointer.
            LlvmValue stringPtr = this.builder.CreateGlobalString(node.Value, name, true);

            // Append the pointer value onto the stack.
            this.valueStack.Push(stringPtr);

            // Return the node.
            return node;
        }

        public Construct VisitIf(If node)
        {
            // TODO: Action and alternative blocks not being handled, for debugging purposes.

            // Visit the condition.
            this.Visit(node.Condition);

            // Pop the condition off the stack.
            LlvmValue conditionValue = this.valueStack.Pop();

            // Create a zero-value double for the boolean comparison.
            LlvmValue zero = LlvmFactory.Double(0);

            // TODO: Hard-coded name.
            // Build the comparison, condition will be convered to a boolean for a 'ONE' (non-equal) comparison.
            LlvmValue comparison = LLVM.BuildFCmp(this.builder.Unwrap(), LLVMRealPredicate.LLVMRealONE, conditionValue.Unwrap(), zero.Unwrap(), "ifcond").Wrap();

            // Retrieve the parent function from the builder.
            LlvmFunction function = this.builder.Block.Parent;

            // Create the action block.
            LlvmBlock action = function.AppendBlock("then");

            // TODO: Debugging, Ret void for action.
            action.Builder.CreateReturnVoid();

            LlvmBlock otherwise = function.AppendBlock("else");

            // TODO: Debugging, ret void for otherwise.
            otherwise.Builder.CreateReturnVoid();

            LlvmBlock merge = function.AppendBlock("ifcont");

            // TODO: Debugging, ret void for merge.
            merge.Builder.CreateReturnVoid();

            // Build the if construct.
            LlvmValue @if = LLVM.BuildCondBr(this.builder.Unwrap(), comparison.Unwrap(), action.Unwrap(), otherwise.Unwrap()).Wrap();

            // TODO: Complete implementation, based off: https://github.com/microsoft/LLVMSharp/blob/master/KaleidoscopeTutorial/Chapter5/KaleidoscopeLLVM/CodeGenVisitor.cs#L214
            // ...

            // TODO: Debugging, not complete.
            action.Builder.PositionAtEnd(); // ? Delete..

            // Append the if construct onto the stack.
            this.valueStack.Push(@if);

            // Return the node.
            return node;
        }

        public Construct VisitArray(Constructs.Array node)
        {
            // Prepare the value buffer list.
            List<LlvmValue> values = new List<LlvmValue>();

            // Iterate and emit all the values onto the buffer list.
            foreach (Value value in node.Values)
            {
                // Visit the value.
                this.VisitValue(value);

                // Pop the value off the stack.
                LlvmValue llvmValue = this.valueStack.Pop();

                // Append the value onto the buffer list.
                values.Add(llvmValue);
            }

            // Visit the kind.
            this.VisitKind(node.Kind);

            // Pop the type off the stack.
            LlvmType elementType = this.typeStack.Pop();

            // Create the array.
            LlvmValue array = LlvmFactory.Array(elementType, values.ToArray());

            // Append the array onto the stack.
            this.valueStack.Push(array);

            // Return the node.
            return node;
        }

        public Construct VisitGlobal(Global node)
        {
            // Visit the kind.
            this.VisitKind(node.Kind);

            // Pop the type off the stack.
            LlvmType type = this.typeStack.Pop();

            // Create the global variable.
            LlvmGlobal global = this.module.CreateGlobal(node.Identifier, type);

            // Set the linkage to common.
            global.SetLinkage(LLVMLinkage.LLVMCommonLinkage);

            // Assign initial value if applicable.
            if (node.InitialValue != null)
            {
                // Visit the initial value.
                this.Visit(node.InitialValue);

                // Pop off the initial value off the stack.
                LlvmValue initialValue = this.valueStack.Pop();

                // Set the initial value.
                global.SetInitialValue(initialValue);
            }

            // Append the global onto the stack.
            this.valueStack.Push(global);

            // Return the node.
            return node;
        }

        public Construct VisitExtern(Extern node)
        {
            // Ensure prototype is set.
            if (node.Prototype == null)
            {
                throw new Exception("Unexpected external definition's prototype to be null");
            }

            // Create the argument buffer list.
            List<LlvmType> arguments = new List<LlvmType>();

            // TODO: What about reference? Arguments must be named for extern?
            foreach ((Kind kind, Reference reference) in node.Prototype.Arguments)
            {
                // Visit the kind.
                this.VisitKind(kind);

                // Pop the type off the stack.
                LlvmType argumentType = this.typeStack.Pop();

                // Append onto the arguments list.
                arguments.Add(argumentType);
            }

            // Visit the prototype's return kind.
            this.Visit(node.Prototype.ReturnKind);

            // Pop the return type off the stack.
            LlvmType returnType = this.typeStack.Pop();

            // Emit the function type.
            LlvmType type = LlvmFactory.Function(returnType, arguments.ToArray(), node.Prototype.HasInfiniteArguments);

            // Emit the external definition to context and capture the LLVM value reference.
            LlvmValue @extern = this.module.CreateFunction(node.Prototype.Identifier, type);

            // Determine if should be registered on the symbol table.
            if (!this.module.ContainsFunction(node.Prototype.Identifier))
            {
                // Register the external definition as a function in the symbol table.
                this.module.RegisterFunction((LlvmFunction)@extern);
            }
            // Otherwise, throw an error.
            else
            {
                throw new Exception($"Warning: Extern definition '{node.Prototype.Identifier}' being re-defined");
            }

            // Push the resulting value onto the stack.
            this.valueStack.Push(@extern);

            // Return the node.
            return node;
        }

        public Construct VisitValue(Value node)
        {
            // Create the value buffer.
            LlvmValue value;

            // Ensure value is identified as a literal.
            if (!Recognition.IsLiteral(node.Content))
            {
                throw new Exception("Content could not be identified as a valid literal");
            }
            // Integer literal.
            else if (Recognition.IsInteger(node.Content))
            {
                // Visit the kind.
                this.VisitKind(node.Kind);

                // Pop the resulting type off the stack.
                LlvmType type = this.typeStack.Pop();

                // Create the type and assign the value buffer.
                value = LlvmFactory.Int(type, int.Parse(node.Content));
            }
            // String literal.
            else if (Recognition.IsStringLiteral(node.Content))
            {
                value = LlvmFactory.String(node.Content);
            }
            // Unrecognized literal.
            else
            {
                throw new Exception($"Unrecognized literal: {node.Content}");
            }

            // Append the value onto the stack.
            this.valueStack.Push(value);

            // Return the node.
            return node;
        }

        public Construct VisitKind(Kind node)
        {
            // Create the initial type.
            LlvmType type = TokenConstants.kindGenerationMap[node.Type]().Wrap();

            // Convert to a pointer if applicable.
            if (node.IsPointer)
            {
                type.ConvertToPointer();
            }

            // Append the resulting type onto the stack.
            this.typeStack.Push(type);

            // Return the node.
            return node;
        }

        public Construct VisitRoutine(Routine node)
        {
            // Ensure body was provided or created.
            if (node.Body == null)
            {
                throw new Exception("Unexpected function body to be null");
            }
            // Ensure prototype is set.
            else if (node.Prototype == null)
            {
                throw new Exception("Unexpected function prototype to be null");
            }
            // Ensures the function does not already exist.
            else if (this.module.ContainsFunction(node.Prototype.Identifier))
            {
                throw new Exception($"A function with the identifier '{node.Prototype.Identifier}' already exists");
            }

            // Clear named values.
            this.namedValues.Clear();

            // Create an argument buffer list.
            List<LlvmType> arguments = new List<LlvmType>();

            // Process the prototype's arguments.
            foreach ((Kind kind, Reference reference) in node.Prototype.Arguments)
            {
                // Visit the argument's type.
                this.Visit(kind);

                // Pop the resulting type off the stack.
                LlvmType argumentType = this.typeStack.Pop();

                // Append the argument's type to the argument list.
                arguments.Add(argumentType);
            }

            // Visit the return type node.
            this.Visit(node.Prototype.ReturnKind);

            // Pop off the return type off the stack.
            LlvmType returnType = this.typeStack.Pop();

            // Emit the function type.
            LlvmType type = LlvmFactory.Function(returnType, arguments.ToArray(), node.Prototype.HasInfiniteArguments);

            // Create the function.
            LlvmFunction function = this.module.CreateFunction(node.Prototype.Identifier, type);

            // Register as the temporary, local function.
            this.function = function;

            // Create the argument index counter.
            uint argumentIndexCounter = 0;

            // Name arguments.
            foreach ((Kind kind, Reference reference) in node.Prototype.Arguments)
            {
                // Retrieve the argument.
                LlvmValue argument = function.GetArgumentAt(argumentIndexCounter);

                // Name the argument.
                argument.SetName(reference.Value);

                // Increment the index counter for next iteration.
                argumentIndexCounter++;
            }

            // Visit the body.
            this.VisitSection(node.Body);

            // Pop the body off the stack.
            this.blockStack.Pop();

            // TODO
            // Ensure that body returns a value if applicable.
            // else if (!node.Prototype.ReturnKind.IsVoid && !node.Body.HasReturnValue)
            // {
            //     throw new Exception("Functions that do not return void must return a value");
            // }

            // Verify the function.
            function.Verify();

            // Append the function onto the stack.
            this.valueStack.Push(function);

            // Return the node.
            return node;
        }

        public Construct VisitInteger(Integer node)
        {
            // Visit the kind.
            this.VisitKind(node.Kind);

            // Pop the resulting type off the stack.
            LlvmType type = this.typeStack.Pop();

            // Convert to a constant and return as an llvm value wrapper instance.
            LlvmValue value = LlvmFactory.Int(type, node.Value);

            // Push the value onto the stack.
            this.valueStack.Push(value);

            // Return the node.
            return node;
        }

        public Construct VisitVariable(Variable node)
        {
            // Create the value buffer.
            LlvmValue value;

            // Attempt to lookup the value by its name.
            if (this.namedValues.TryGetValue(node.Name, out value))
            {
                // Append the value to the stack.
                this.valueStack.Push(value);
            }
            // At this point, reference variable is non-existent.
            else
            {
                throw new Exception($"Undefined reference to variable: '{node.Name}'");
            }

            // Return the node.
            return node;
        }

        public Construct VisitPrototype(Prototype node)
        {
            // Retrieve argument count within node.
            uint argumentCount = (uint)node.Arguments.Length;

            // Create the argument buffer array.
            LlvmType[] arguments = new LlvmType[Math.Max(argumentCount, 1)];

            // Attempt to retrieve an existing function value.
            LlvmFunction? function = this.module.GetFunction(node.Identifier);

            // Function may be already defined.
            if (function != null)
            {
                // Function already has a body, disallow re-definition.
                if (function.HasBlocks)
                {
                    throw new Exception($"Cannot re-define function: {node.Identifier}");
                }
                // If the function takes a different number of arguments, reject.
                else if (function.ArgumentCount != argumentCount)
                {
                    throw new Exception("redefinition of function with different # args");
                }
            }
            else
            {
                // TODO: Wrong type.
                for (int i = 0; i < argumentCount; ++i)
                {
                    arguments[i] = LLVM.DoubleType().Wrap();
                }

                // TODO: Support for infinite arguments and hard-coded return type.
                // Create the function type.
                LlvmType type = LlvmFactory.Function(LlvmFactory.Void(), arguments, false);

                // Create the function within the module.
                function = this.module.CreateFunction(node.Identifier, type);

                // Set the function's linkage.
                function.SetLinkage(LLVMLinkage.LLVMExternalLinkage);
            }

            // Process arguments.
            for (int i = 0; i < argumentCount; ++i)
            {
                // Retrieve the argument name.
                string argumentName = node.Arguments[i].Item2.Value;

                // Retrieve the argument at the current index iterator.
                LlvmValue argument = function.GetArgumentAt((uint)i);

                // Name the argument.
                argument.SetName(argumentName);

                // TODO: Watch out for already existing ones.
                // Stored the named argument in the named values cache.
                this.namedValues.Add(argumentName, argument);
            }

            // Push the function onto the stack.
            this.valueStack.Push(function);

            // Return the node.
            return node;
        }

        public Construct VisitSection(Section node)
        {
            // Ensure function buffer is not null.
            if (this.function == null)
            {
                throw new Exception("Expected function buffer to be set");
            }

            // Create the block.
            LlvmBlock block = this.function.AppendBlock(node.Identifier);

            // Set builder.
            this.builder = block.Builder;

            // Process the section's instructions.
            foreach (Instruction inst in node.Instructions)
            {
                // Visit the instruction.
                this.Visit(inst);

                // Pop the resulting LLVM instruction off the stack.
                this.valueStack.Pop();
            }

            // Append the block onto the stack.
            this.blockStack.Push(block);

            // Return the node.
            return node;
        }

        public Construct VisitBinaryExpr(BinaryExpr node)
        {
            // Visit left side.
            this.Visit(node.LeftSide);

            // Visit right side.
            this.Visit(node.RightSide);

            // Pop right side off the stack.
            LlvmValue rightSide = this.valueStack.Pop();

            // Pop left side off the stack.
            LlvmValue leftSide = this.valueStack.Pop();

            // Create a value buffer.
            LlvmValue binaryExpr = this.builder.CreateAdd(leftSide, rightSide, node.ResultIdentifier);

            // Push the resulting value onto the stack.
            this.valueStack.Push(binaryExpr);

            // Return the node.
            return node;
        }
    }
}
