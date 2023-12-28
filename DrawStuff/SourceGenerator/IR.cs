
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace ShaderCompiler;

public record NamedValue(string Name, TypeTag Type);

public interface TypeTag { }
public interface BuiltinType : TypeTag { }

public interface PrimitiveType : TypeTag { }

public record FloatType() : PrimitiveType;
public record Vec2Type() : PrimitiveType;
public record Vec3Type() : PrimitiveType;
public record Vec4Type() : PrimitiveType;
public record UInt32Type() : PrimitiveType;
public record Mat4Type() : PrimitiveType;
public record RGBAType() : PrimitiveType;
public record TextureType() : BuiltinType;
public record VoidType() : BuiltinType;

public static class BuiltinTypes {
    public static FloatType Float = new();
    public static Vec2Type Vec2 = new();
    public static Vec3Type Vec3 = new();
    public static Vec4Type Vec4 = new();
    public static UInt32Type UInt32 = new();
    public static Mat4Type Mat4 = new();
    public static RGBAType RGBA = new();
    public static TextureType Texture2D = new();
    public static VoidType Void = new();
}

public record CustomStruct(
    string Name,
    string FullName,
    bool HasSequentialAttrib,
    ImmutableArray<(string Name, TypeTag Type)> Fields,
    Location Loc
) : TypeTag;

public static class IR {

    public enum Op {
        Plus, Minus, Multiply, Divide, Modulo,
        ShiftLeft, ShiftRight, BitAnd, BitOr,
        Equals, Not, NotEquals, LessThan, GreaterThan,
        GreaterThanOrEqual, LessThanOrEqual
    }

    public enum IntrinsicOp {
        TextureSample,
        RGBAConstruct,
        Discard,
    }

    public interface Expr {
        public record Error : Expr;
        public record FieldAccess(Expr Obj, string FieldName) : Expr;
        public record Assignment(Expr Target, Expr Value) : Expr;
        public record Construct(TypeTag Type, ImmutableArray<Expr> Args) : Expr;
        public record Invoke(Expr Func, ImmutableArray<Expr> Args) : Expr;
        public record BinOp(Expr Left, Op Operator, Expr Right) : Expr;
        public record PrefixOp(Op Operator, Expr Value) : Expr;
        public record Paren(Expr Expr) : Expr;
        public record Identifier(string Name) : Expr;
        public record Intrinsic(IntrinsicOp Op) : Expr;
        public record LiteralFloat(float Value) : Expr;
        public record LiteralBool(bool Value) : Expr;
        public record LiteralI32(int Value) : Expr;
        public record LiteralU32(uint Value) : Expr;
    };


    public interface Statement {
        public record Error : Statement;
        public record DeclareLocal(TypeTag Type, string Name, Expr? Value) : Statement;
        public record Expression(Expr Expr) : Statement;
        public record Return(Expr? Value) : Statement;
        public record Block(ImmutableArray<Statement> Statements) : Statement;
        public record If(Expr Condition, Statement ThenDo, Statement? ElseDo) : Statement;    
    }


    public record Function(
        string Name,
        TypeTag ReturnType,
        ImmutableArray<NamedValue> Args,
        Statement.Block Body);

    public record Shader(
        ImmutableArray<NamedValue> Globals,
        ImmutableArray<CustomStruct> CustomStructs,
        ImmutableArray<Function> HelperFunctions,
        Function EntryFunction);

    public record Program(Shader Vertex, Shader Fragment);
}
