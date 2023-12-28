using System;
using System.Text;

namespace ShaderCompiler;

public class SrcWriter {

    public struct IndentScope : IDisposable {
        int indent;
        SrcWriter w;
        public IndentScope(SrcWriter w, int indent) {
            (this.w, this.indent) = (w, indent);
            w.indent += indent;
        }
        public void Dispose() {
            w.indent -= indent;
        }
    }

    bool lineStarted = false;
    int indent = 0;
    int defaultIndentSize;
    StringBuilder stringBuilderInstance;

    private void AppendIndent() {
        for (int i = 0; i < indent; ++i) stringBuilderInstance.Append(' ');
        lineStarted = true;
    }

    private void Append(string s) {
        if (!lineStarted)
            AppendIndent();
        stringBuilderInstance.Append(s);
    }

    private void AppendLine(string s) {
        Append(s);
        stringBuilderInstance.AppendLine();
        lineStarted = false;
    }

    public IndentScope Indent(int indent) => new(this, indent);
    public IndentScope Indent() => new(this, defaultIndentSize);

    public SrcWriter Write(string code) {
        Append(code);
        return this;
    }

    public SrcWriter WriteLine(string code) {
        AppendLine(code);
        return this;
    }

    public SrcWriter WriteLine() {
        AppendLine("");
        return this;
    }

    public SrcWriter(int defaultIndentSize = 4) {
        indent = 0;
        stringBuilderInstance = new StringBuilder();
        this.defaultIndentSize = defaultIndentSize;
    }

    public override string ToString() => stringBuilderInstance.ToString();
}

