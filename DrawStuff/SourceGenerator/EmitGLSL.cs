
using System.Collections.Generic;
using System.Linq;

namespace ShaderCompiler.GL;

public enum CompileMode {
    FragmentEntryPoint,
    VertexEntryPoint,
    SharedFunction,
}

class EmitGLSL {

    SrcWriter Writer = new();

    IR.Program Program;

    IR.Function Function;

    CompileMode Mode;

    NamedValue? OutputVar = null;

    public EmitGLSL(SrcWriter writer, IR.Program program, IR.Function function, CompileMode mode)
    {
        Writer = writer;
        Program = program;
        Function = function;
        Mode = mode;
        var fragArgs = program.Fragment.EntryFunction.Args;
        OutputVar = mode switch {
            CompileMode.VertexEntryPoint =>
                new(
                    fragArgs.IsEmpty
                        ? "_fragment_input"
                        : $"_fragment_input_{fragArgs[0].Name}",
                    Function.ReturnType),
            CompileMode.FragmentEntryPoint => new("out_color", BuiltinTypes.RGBA),
            _ => null,
        };
    }

    EmitGLSL Emit(string code) {
        Writer.Write(code);
        return this;
    }

    EmitGLSL EmitLine(string code) {
        Writer.WriteLine(code);
        return this;
    }

    EmitGLSL EmitLine() {
        Writer.WriteLine();
        return this;
    }

    SrcWriter.IndentScope WithIndent() => Writer.Indent();

    EmitGLSL EmitBlock(IR.Statement.Block block) {
        foreach(var s in block.Statements)
            EmitStatement(s);
        return this;
    }

    EmitGLSL EmitReturn(IR.Statement.Return e) {
        if (e.Value != null) {
            if (OutputVar != null) {
                Emit(OutputVar.Name).Emit(" = ").EmitExpr(e.Value!).EmitLine(";");
                if (Mode is CompileMode.VertexEntryPoint) {
                    if (Function.ReturnType is Vec4Type)
                        EmitLine($"gl_Position = {OutputVar.Name};");
                    else
                        EmitLine($"gl_Position = {OutputVar.Name}.Pos;");
                }
                return EmitLine("return;");
            }
            else {
                return Emit("return ").EmitExpr(e.Value).EmitLine(";");
            }
        }
        else {
            return EmitLine("return;");
        }
    }

    EmitGLSL EmitIf(IR.Statement.If ifs) {
        Emit("if (").EmitExpr(ifs.Condition).EmitLine(") {");
        using (var _ = WithIndent())
            EmitStatement(ifs.ThenDo);
        EmitLine("}");
        if (ifs.ElseDo is IR.Statement elseDo) {
            EmitLine("else {");
            using (var _ = WithIndent())
                EmitStatement(elseDo);
            EmitLine("}");
        }
        return this;
    }

    EmitGLSL EmitDeclareLocal(IR.Statement.DeclareLocal l) {
        EmitType(l.Type).Emit($" {l.Name}");
        return l.Value switch {
            null => EmitLine(";"),
            _ => Emit(" = ").EmitExpr(l.Value).EmitLine(";"),
        };
    }

    EmitGLSL EmitStatement(IR.Statement s) => s switch {
        IR.Statement.Block b => EmitBlock(b),
        IR.Statement.Expression e => EmitExpr(e.Expr).EmitLine(";"),
        IR.Statement.DeclareLocal l => EmitDeclareLocal(l),
        IR.Statement.If ifs => EmitIf(ifs),
        IR.Statement.Return ret => EmitReturn(ret),
        _ => throw new($"encounted unsupported statement type '{s.GetType().Name}'"),
    };

    static string OpToString(IR.Op op) => op switch {
        IR.Op.Plus => "+",
        IR.Op.Minus => "-",
        IR.Op.Multiply => "*",
        IR.Op.Divide => "/",
        IR.Op.Modulo => "%",
        IR.Op.ShiftLeft => "<<",
        IR.Op.ShiftRight => ">>",
        IR.Op.BitAnd => "&",
        IR.Op.BitOr => "|",
        IR.Op.Equals => "==",
        IR.Op.Not => "!",
        IR.Op.NotEquals => "!=",
        IR.Op.LessThan => "<",
        IR.Op.GreaterThan => ">",
        IR.Op.GreaterThanOrEqual => ">=",
        IR.Op.LessThanOrEqual => "<=",
        _ => throw new ($"unexpected op type {op}"),
    };

    EmitGLSL EmitType(TypeTag t) => Emit(TypeToString(t));

    EmitGLSL EmitExprList(IEnumerable<IR.Expr> es, string sep) {
        if (es.Any()) {
            EmitExpr(es.First());
            foreach (var e in es.Skip(1)) {
                Emit(sep).EmitExpr(e);
            }
        }
        return this;
    }

    EmitGLSL EmitIntrinsic(IR.Expr.Intrinsic i) => i.Op switch {
        IR.IntrinsicOp.TextureSample => Emit("texture"),
        IR.IntrinsicOp.RGBAConstruct => Emit("vec4"),
        IR.IntrinsicOp.Discard => Emit("discard"),
        _ => throw new($"unexpected intrinsic {i.Op}"),
    };

    EmitGLSL EmitExpr(IR.Expr e) => e switch {

        IR.Expr.Assignment ae =>
            EmitExpr(ae.Target).Emit(" = ").EmitExpr(ae.Value),

        IR.Expr.BinOp bo =>
            EmitExpr(bo.Left).Emit($" {OpToString(bo.Operator)} ").EmitExpr(bo.Right),

        IR.Expr.PrefixOp po =>
            Emit(OpToString(po.Operator)).EmitExpr(po.Value),

        IR.Expr.FieldAccess fa =>
            EmitExpr(fa.Obj).Emit(".").Emit(fa.FieldName),

        IR.Expr.Construct c =>
            EmitType(c.Type).Emit("(").EmitExprList(c.Args, ", ").Emit(")"),

        IR.Expr.Invoke inv =>
            EmitExpr(inv.Func).Emit("(").EmitExprList(inv.Args, ", ").Emit(")"),

        IR.Expr.LiteralFloat lf => Emit($"{lf.Value}"),
        IR.Expr.LiteralBool lb => Emit($"{lb.Value}"),
        IR.Expr.LiteralI32 li => Emit($"{li.Value}"),
        IR.Expr.LiteralU32 lu => Emit($"{lu.Value}u"),
        IR.Expr.Identifier id => Emit(id.Name),
        IR.Expr.Paren pe => Emit("(").EmitExpr(pe.Expr).Emit(")"),
        IR.Expr.Intrinsic intr => EmitIntrinsic(intr),
        _ => throw new($"unknown expr type {e.GetType().Name}, value: {e}"),
    };

    static string TypeToString(TypeTag t) => t switch {
        VoidType => "void",
        FloatType => "float",
        Vec2Type => "vec2",
        Vec3Type => "vec3",
        Vec4Type => "vec4",
        UInt32Type => "uint",
        Mat4Type => "mat4",
        RGBAType => "vec4",
        TextureType => "sampler2D",
        CustomStruct cs => cs.Name,
        _ => throw new ShaderGenException($"unknown type '{t}'"),
    };

    void EmitHeader(IR.Shader shader) {
        EmitLine("#version 330 core");
        foreach (var u in shader.Globals) {
            EmitLine($"uniform {TypeToString(u.Type)} {u.Name};");
        }
        EmitLine();
        foreach(var cs in shader.CustomStructs) {
            EmitLine($"struct {cs.Name} {{");
            foreach(var (n, t) in cs.Fields) {
                EmitLine($"    {TypeToString(t)} {n};");
            }
            EmitLine("};");
            EmitLine();
        }
        if (Mode is CompileMode.VertexEntryPoint) {
            int vertexAttribLocation = 0;
            foreach (var a in Function.Args) {
                if(a.Type is CustomStruct cs) {
                    foreach(var f in cs.Fields) {
                        EmitLine(
                            $"layout (location = {vertexAttribLocation++})"
                            + $" in {TypeToString(f.Type)} _attrib_{a.Name}_{f.Name};");
                    }
                }
                else {
                    EmitLine(
                        $"layout (location = {vertexAttribLocation++})"
                        + $" in {TypeToString(a.Type)} _attrib_{a.Name};");
                }
            }
        }
        else {
            foreach (var i in Function.Args) {
                if(i.Type is UInt32Type && Mode is CompileMode.FragmentEntryPoint)
                    Emit($"flat ");
                EmitLine($"in {TypeToString(i.Type)} _fragment_input_{i.Name};");
            }
        }
        if(OutputVar != null) {
            if (OutputVar.Type is UInt32Type && Mode is CompileMode.VertexEntryPoint)
                Emit($"flat ");
            EmitLine($"out {TypeToString(OutputVar.Type)} {OutputVar.Name};");
        }
    }

    EmitGLSL EmitFunction() {
        if(Mode is CompileMode.VertexEntryPoint) {
            EmitLine("void main() {");
            using (WithIndent()) {
                foreach (var a in Function.Args) {
                    if(a.Type is CustomStruct cs) {
                        var argStr = string.Join(", ",
                            cs.Fields.Select(f => $"_attrib_{a.Name}_{f.Name}"));
                        EmitLine($"{TypeToString(a.Type)} {a.Name}"
                            + $" = {TypeToString(a.Type)}({argStr});");
                    }
                    else {
                        EmitLine($"{TypeToString(a.Type)} {a.Name}"
                            + $" = _attrib_{a.Name};");
                    }
                }
                EmitBlock(Function.Body);
            }
            EmitLine("}");
        }
        else if (Mode is CompileMode.FragmentEntryPoint) {
            EmitLine("void main() {");
            using (WithIndent()) {
                foreach (var a in Function.Args) {
                    EmitLine($"{TypeToString(a.Type)} {a.Name}"
                        + $" = _fragment_input_{a.Name};");
                }
                EmitBlock(Function.Body);
            }
            EmitLine("}");
        }
        else {
            var args = Function.Args.Select(a => $"{TypeToString(a.Type)} {a.Name}");
            EmitType(Function.ReturnType).Emit(" ").Emit(Function.Name)
                .Emit("(")
                .Emit(string.Join(", ", args))
                .EmitLine(") {");
            using (var _ = WithIndent())
                EmitBlock(Function.Body);
            EmitLine("}");
        }
        return this;
    }

    EmitGLSL EmitHelpers(IR.Shader shader) {
        foreach(var h in shader.HelperFunctions) {
            var cg = new EmitGLSL(Writer, Program, h, CompileMode.SharedFunction);
            cg.EmitFunction();
        }
        return this;
    }

    public override string ToString() => Writer.ToString();

    public static (string Vertex, string Fragment) Emit(IR.Program program)
    {
        var vertEmit = new EmitGLSL(
            new(), program, program.Vertex.EntryFunction, CompileMode.VertexEntryPoint);
        vertEmit.EmitHeader(program.Vertex);
        vertEmit.EmitHelpers(program.Vertex);
        vertEmit.EmitFunction();

        var fragEmit = new EmitGLSL(
            new(), program, program.Fragment.EntryFunction, CompileMode.FragmentEntryPoint);
        fragEmit.EmitHeader(program.Fragment);
        fragEmit.EmitHelpers(program.Fragment);
        fragEmit.EmitFunction();

        return (vertEmit.ToString(), fragEmit.ToString());
    }
}
