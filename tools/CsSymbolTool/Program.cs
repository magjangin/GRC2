using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CsSymbolTool;

internal class Program
{
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintUsage();
            return 0;
        }

        string command = args[0].ToLowerInvariant();
        try
        {
            return command switch
            {
                "outline" => RunOutline(args.Skip(1).ToArray()),
                "find" or "search" => RunFind(args.Skip(1).ToArray()),
                "extract" or "dump" => RunExtract(args.Skip(1).ToArray()),
                "scan" => RunScan(args.Skip(1).ToArray()),
                "skeleton" or "export" => RunSkeleton(args.Skip(1).ToArray()),
                _ => HandleUnknown(command)
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Error] {ex.Message}");
            return 1;
        }
    }

    private static int HandleUnknown(string cmd)
    {
        Console.Error.WriteLine($"Unknown command '{cmd}'.");
        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine(@"CsSymbolTool - C# Outline & Symbol Token Saver (Roslyn AST Based)
Usage:
  dotnet run --project tools/CsSymbolTool -- <command> [options]

Commands:
  outline <path> [options]
      파일이나 디렉터리의 C# 클래스/메서드 시그니처와 라인 범위만 압축 출력 (바디 생략, 토큰 95% 절약)
      Options:
        --min-lines <N>   : N줄 이상인 메서드/멤버만 표시 (기본: 1)
        --compact         : 필드 생략 및 시그니처 축약
        --pattern <glob>  : 대상 파일 필터 (기본: *.cs)
        --out <dir>       : 지정 폴더에 Roslyn 기반 스켈레톤 .cs 파일 전체 출력

  skeleton [src] [out] [options]
      프로젝트의 모든 .cs 파일에서 메서드 몸체를 비운 C# 스켈레톤 코드를 생성하여 대상 폴더에 내보냄
      (기본값: src=GRC2, out=GRC2-outline)

  find <query> [options]
      디렉터리 내에서 클래스/메서드/프로퍼티 심볼명을 고속 검색 (경로:라인 및 시그니처 반환)
      Options:
        --path <dir>      : 검색 시작 경로 (기본: 현재 경로)
        --kind <filter>   : method | class | property | all (기본: all)
        --regex           : 검색어를 정규식으로 처리

  extract <file> <symbol_or_line>
      특정 파일에서 특정 심볼(메서드명) 또는 라인에 해당하는 코드 블록만 정확히 잘라내서 출력
      (AI에게 줄 핵심 코드 조각만 복사하여 토큰 낭비 방지)
      Examples:
        extract Decompiled/.../Game.cs OnHitNote
        extract GRC2/Core/Chart.cs 120

  scan [path] [options]
      프로젝트 내 가장 긴 메서드 Top N 순위 분석 (Roslyn AST 기반 정밀 측정)
      Options:
        --top <N>         : 출력할 상위 개수 (기본: 30)
        --min-lines <N>   : 최소 라인 수 (기본: 30)
        --path <dir>      : 스캔 대상 경로 (기본: GRC2 또는 현재 폴더)
");
    }

    #region Outline Command

    private static int RunOutline(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Error: path is required for outline.");
            return 1;
        }

        string target = args[0];
        int minLines = 1;
        bool compact = false;
        string pattern = "*.cs";
        string? outDir = null;

        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--min-lines" && i + 1 < args.Length)
                minLines = int.Parse(args[++i]);
            else if (args[i] == "--compact")
                compact = true;
            else if (args[i] == "--pattern" && i + 1 < args.Length)
                pattern = args[++i];
            else if ((args[i] == "--out" || args[i] == "--out-dir") && i + 1 < args.Length)
                outDir = args[++i];
        }

        if (!string.IsNullOrEmpty(outDir))
        {
            return ExportSkeleton(target, outDir, pattern);
        }

        var files = GetFiles(target, pattern);
        if (files.Count == 0)
        {
            Console.WriteLine($"No .cs files found in: {target}");
            return 0;
        }

        foreach (var file in files)
        {
            OutlineFile(file, minLines, compact);
        }

        return 0;
    }

    private static void OutlineFile(string filePath, int minLines, bool compact)
    {
        string text = File.ReadAllText(filePath, Encoding.UTF8);
        var tree = CSharpSyntaxTree.ParseText(text);
        var root = tree.GetRoot();
        int totalLines = tree.GetLineSpan(root.FullSpan).EndLinePosition.Line + 1;

        var typeNodes = root.DescendantNodes()
            .OfType<BaseTypeDeclarationSyntax>()
            .ToList();

        if (typeNodes.Count == 0) return;

        Console.WriteLine($"\n[File] {GetRelative(filePath)} ({totalLines} lines)");

        foreach (var typeNode in typeNodes)
        {
            // 중첩 타입이 아닌 최상위 또는 부모 타입만 순회 (자식은 재귀로)
            if (typeNode.Parent is BaseTypeDeclarationSyntax) continue;
            PrintTypeHierarchy(typeNode, tree, minLines, compact, indent: "  ");
        }
    }

    private static void PrintTypeHierarchy(BaseTypeDeclarationSyntax typeNode, SyntaxTree tree, int minLines, bool compact, string indent)
    {
        var span = tree.GetLineSpan(typeNode.Span);
        int start = span.StartLinePosition.Line + 1;
        int end = span.EndLinePosition.Line + 1;
        int lines = end - start + 1;

        string kind = typeNode switch
        {
            ClassDeclarationSyntax => "class",
            StructDeclarationSyntax => "struct",
            InterfaceDeclarationSyntax => "interface",
            RecordDeclarationSyntax => "record",
            EnumDeclarationSyntax => "enum",
            _ => "type"
        };

        string name = typeNode.Identifier.Text;
        string typeParams = "";
        if (typeNode is TypeDeclarationSyntax td && td.TypeParameterList != null)
        {
            typeParams = td.TypeParameterList.ToString();
        }

        string baseList = typeNode.BaseList != null ? $" : {typeNode.BaseList.Types}" : "";
        string mods = typeNode.Modifiers.ToString();
        if (!string.IsNullOrEmpty(mods)) mods += " ";

        Console.WriteLine($"{indent}{mods}{kind} {name}{typeParams}{baseList} [L{start}-L{end}, {lines} lines]");

        if (typeNode is EnumDeclarationSyntax enumSyntax)
        {
            if (!compact)
            {
                var members = string.Join(", ", enumSyntax.Members.Select(m => m.Identifier.Text));
                if (members.Length > 80) members = members.Substring(0, 77) + "...";
                Console.WriteLine($"{indent}    Values: {members}");
            }
            return;
        }

        if (typeNode is not TypeDeclarationSyntax typeDecl) return;

        // 멤버 순회
        foreach (var member in typeDecl.Members)
        {
            if (member is BaseTypeDeclarationSyntax nestedType)
            {
                PrintTypeHierarchy(nestedType, tree, minLines, compact, indent + "  ");
                continue;
            }

            var mSpan = tree.GetLineSpan(member.Span);
            int mStart = mSpan.StartLinePosition.Line + 1;
            int mEnd = mSpan.EndLinePosition.Line + 1;
            int mLines = mEnd - mStart + 1;

            if (mLines < minLines) continue;

            string memberIndent = indent + "  ";
            switch (member)
            {
                case MethodDeclarationSyntax method:
                    string mSig = $"{method.Modifiers} {method.ReturnType} {method.Identifier}{method.TypeParameterList}{method.ParameterList}".Trim();
                    Console.WriteLine($"{memberIndent}{mSig} [L{mStart}-L{mEnd}, {mLines} lines]");
                    break;

                case ConstructorDeclarationSyntax ctor:
                    string cSig = $"{ctor.Modifiers} {ctor.Identifier}{ctor.ParameterList}".Trim();
                    Console.WriteLine($"{memberIndent}{cSig} [L{mStart}-L{mEnd}, {mLines} lines]");
                    break;

                case PropertyDeclarationSyntax prop:
                    string pSig = $"{prop.Modifiers} {prop.Type} {prop.Identifier}".Trim();
                    string accessors = prop.AccessorList != null 
                        ? $" {{ {string.Join(" ", prop.AccessorList.Accessors.Select(a => a.Keyword.Text + ";"))} }}" 
                        : (prop.ExpressionBody != null ? " => ...;" : ";");
                    Console.WriteLine($"{memberIndent}{pSig}{accessors} [L{mStart}-L{mEnd}, {mLines} lines]");
                    break;

                case FieldDeclarationSyntax field when !compact:
                    string fVars = string.Join(", ", field.Declaration.Variables.Select(v =>
                        field.Modifiers.Any(SyntaxKind.ConstKeyword) && v.Initializer != null
                            ? $"{v.Identifier} = {v.Initializer.Value}"
                            : v.Identifier.Text));
                    string fSig = $"{field.Modifiers} {field.Declaration.Type} {fVars}".Trim();
                    Console.WriteLine($"{memberIndent}{fSig}; [L{mStart}]");
                    break;

                case EventFieldDeclarationSyntax evt:
                    string eVars = string.Join(", ", evt.Declaration.Variables.Select(v => v.Identifier.Text));
                    string eSig = $"{evt.Modifiers} event {evt.Declaration.Type} {eVars}".Trim();
                    Console.WriteLine($"{memberIndent}{eSig}; [L{mStart}]");
                    break;
            }
        }
    }

    #endregion

    #region Find Command

    private static int RunFind(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Error: query is required for find.");
            return 1;
        }

        string query = args[0];
        string targetPath = ".";
        string kindFilter = "all";
        bool isRegex = false;

        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--path" && i + 1 < args.Length)
                targetPath = args[++i];
            else if (args[i] == "--kind" && i + 1 < args.Length)
                kindFilter = args[++i].ToLowerInvariant();
            else if (args[i] == "--regex")
                isRegex = true;
        }

        Regex? rx = isRegex ? new Regex(query, RegexOptions.IgnoreCase | RegexOptions.Compiled) : null;
        bool Matches(string name) => rx != null ? rx.IsMatch(name) : name.Contains(query, StringComparison.OrdinalIgnoreCase);

        var files = GetFiles(targetPath, "*.cs");
        Console.WriteLine($"Searching '{query}' in {files.Count} files...\n");

        int matchCount = 0;
        foreach (var file in files)
        {
            string text = File.ReadAllText(file, Encoding.UTF8);
            if (!text.Contains(query, StringComparison.OrdinalIgnoreCase) && !isRegex)
                continue;

            var tree = CSharpSyntaxTree.ParseText(text);
            var root = tree.GetRoot();

            // 타입 검사
            if (kindFilter is "all" or "class" or "type")
            {
                foreach (var td in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
                {
                    if (Matches(td.Identifier.Text))
                    {
                        var span = tree.GetLineSpan(td.Span);
                        Console.WriteLine($"[Type] {GetRelative(file)}:{span.StartLinePosition.Line + 1}  {td.Modifiers} {td.Identifier}");
                        matchCount++;
                    }
                }
            }

            // 메서드/생성자 검사
            if (kindFilter is "all" or "method")
            {
                foreach (var md in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    if (Matches(md.Identifier.Text))
                    {
                        var span = tree.GetLineSpan(md.Span);
                        int start = span.StartLinePosition.Line + 1;
                        int end = span.EndLinePosition.Line + 1;
                        string parentName = GetParentTypeName(md);
                        string sig = $"{md.ReturnType} {parentName}.{md.Identifier}{md.ParameterList}";
                        Console.WriteLine($"[Method] {GetRelative(file)}:L{start}-L{end} ({end - start + 1} lines)  {sig}");
                        matchCount++;
                    }
                }

                foreach (var cd in root.DescendantNodes().OfType<ConstructorDeclarationSyntax>())
                {
                    if (Matches(cd.Identifier.Text))
                    {
                        var span = tree.GetLineSpan(cd.Span);
                        int start = span.StartLinePosition.Line + 1;
                        int end = span.EndLinePosition.Line + 1;
                        string sig = $"{cd.Identifier}{cd.ParameterList}";
                        Console.WriteLine($"[Ctor] {GetRelative(file)}:L{start}-L{end} ({end - start + 1} lines)  {sig}");
                        matchCount++;
                    }
                }
            }

            // 프로퍼티 검사
            if (kindFilter is "all" or "prop" or "property")
            {
                foreach (var pd in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
                {
                    if (Matches(pd.Identifier.Text))
                    {
                        var span = tree.GetLineSpan(pd.Span);
                        string parentName = GetParentTypeName(pd);
                        Console.WriteLine($"[Property] {GetRelative(file)}:{span.StartLinePosition.Line + 1}  {pd.Type} {parentName}.{pd.Identifier}");
                        matchCount++;
                    }
                }
            }
        }

        Console.WriteLine($"\nTotal matches found: {matchCount}");
        return 0;
    }

    #endregion

    #region Extract Command

    private static int RunExtract(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Error: extract requires <file> and <symbol_or_line>.");
            return 1;
        }

        string file = args[0];
        string target = args[1];

        if (!File.Exists(file))
        {
            Console.Error.WriteLine($"File not found: {file}");
            return 1;
        }

        string text = File.ReadAllText(file, Encoding.UTF8);
        var tree = CSharpSyntaxTree.ParseText(text);
        var root = tree.GetRoot();

        // 1. 숫자로 라인 번호가 지정된 경우
        if (int.TryParse(target, out int lineNo))
        {
            var targetNode = root.DescendantNodes()
                .Where(n => n is MethodDeclarationSyntax or BaseTypeDeclarationSyntax or PropertyDeclarationSyntax or ConstructorDeclarationSyntax)
                .Where(n =>
                {
                    var span = tree.GetLineSpan(n.Span);
                    return span.StartLinePosition.Line + 1 <= lineNo && lineNo <= span.EndLinePosition.Line + 1;
                })
                .OrderBy(n => n.Span.Length)
                .FirstOrDefault();

            if (targetNode != null)
            {
                PrintExtractedNode(targetNode, tree, file);
                return 0;
            }

            Console.Error.WriteLine($"No symbol found enclosing line {lineNo}.");
            return 1;
        }

        // 2. 심볼 이름으로 지정된 경우
        var matchingNodes = root.DescendantNodes()
            .Where(n =>
            {
                return n switch
                {
                    MethodDeclarationSyntax m => m.Identifier.Text.Equals(target, StringComparison.OrdinalIgnoreCase),
                    BaseTypeDeclarationSyntax t => t.Identifier.Text.Equals(target, StringComparison.OrdinalIgnoreCase),
                    PropertyDeclarationSyntax p => p.Identifier.Text.Equals(target, StringComparison.OrdinalIgnoreCase),
                    _ => false
                };
            })
            .ToList();

        if (matchingNodes.Count == 0)
        {
            Console.Error.WriteLine($"Symbol '{target}' not found in {file}.");
            return 1;
        }

        foreach (var node in matchingNodes)
        {
            PrintExtractedNode(node, tree, file);
        }

        return 0;
    }

    private static void PrintExtractedNode(SyntaxNode node, SyntaxTree tree, string file)
    {
        var span = tree.GetLineSpan(node.Span);
        int start = span.StartLinePosition.Line + 1;
        int end = span.EndLinePosition.Line + 1;
        int lines = end - start + 1;

        Console.WriteLine($"// === [Extract] {GetRelative(file)} (L{start}-L{end}, {lines} lines) ===");
        Console.WriteLine(node.ToFullString().Trim());
        Console.WriteLine("// === [End of Extract] ===\n");
    }

    #endregion

    #region Scan Command

    private record MethodMetric(int SpanLines, int StartLine, int EndLine, string Signature, string FilePath);

    private static int RunScan(string[] args)
    {
        string targetPath = "GRC2";
        int topN = 30;
        int minLines = 30;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--top" && i + 1 < args.Length)
                topN = int.Parse(args[++i]);
            else if (args[i] == "--min-lines" && i + 1 < args.Length)
                minLines = int.Parse(args[++i]);
            else if (!args[i].StartsWith("-"))
                targetPath = args[i];
        }

        var files = GetFiles(targetPath, "*.cs");
        var metrics = new List<MethodMetric>();

        foreach (var file in files)
        {
            string text = File.ReadAllText(file, Encoding.UTF8);
            var tree = CSharpSyntaxTree.ParseText(text);
            var root = tree.GetRoot();

            foreach (var node in root.DescendantNodes())
            {
                if (node is MethodDeclarationSyntax md)
                {
                    var span = tree.GetLineSpan(md.Span);
                    int start = span.StartLinePosition.Line + 1;
                    int end = span.EndLinePosition.Line + 1;
                    int lines = end - start + 1;

                    if (lines >= minLines)
                    {
                        string parent = GetParentTypeName(md);
                        string sig = $"{parent}.{md.Identifier}{md.ParameterList}";
                        metrics.Add(new MethodMetric(lines, start, end, sig, file));
                    }
                }
                else if (node is ConstructorDeclarationSyntax cd)
                {
                    var span = tree.GetLineSpan(cd.Span);
                    int start = span.StartLinePosition.Line + 1;
                    int end = span.EndLinePosition.Line + 1;
                    int lines = end - start + 1;

                    if (lines >= minLines)
                    {
                        string parent = GetParentTypeName(cd);
                        string sig = $"{parent}.ctor{cd.ParameterList}";
                        metrics.Add(new MethodMetric(lines, start, end, sig, file));
                    }
                }
            }
        }

        var ranked = metrics.OrderByDescending(m => m.SpanLines).Take(topN).ToList();

        Console.WriteLine($"=== Longest Methods Scan (Top {ranked.Count}) ===");
        Console.WriteLine(" Lines  Location                Method Signature");
        Console.WriteLine("--------------------------------------------------------------------------------");
        foreach (var m in ranked)
        {
            string loc = $"{GetRelative(m.FilePath)}:L{m.StartLine}-L{m.EndLine}";
            Console.WriteLine($"{m.SpanLines,6}  {loc,-30}  {m.Signature}");
        }

        return 0;
    }

    #endregion

    #region Helpers

    private static List<string> GetFiles(string path, string pattern)
    {
        if (File.Exists(path))
            return new List<string> { Path.GetFullPath(path) };

        if (!Directory.Exists(path))
            return new List<string>();

        return Directory.GetFiles(path, pattern, SearchOption.AllDirectories)
            .Where(f => !f.Contains(@"\obj\") && !f.Contains(@"/obj/") &&
                        !f.Contains(@"\bin\") && !f.Contains(@"/bin/"))
            .Select(Path.GetFullPath)
            .ToList();
    }

    private static string GetRelative(string fullPath)
    {
        try
        {
            return Path.GetRelativePath(Directory.GetCurrentDirectory(), fullPath).Replace('\\', '/');
        }
        catch
        {
            return fullPath.Replace('\\', '/');
        }
    }

    private static string GetParentTypeName(SyntaxNode node)
    {
        var parentType = node.Ancestors().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault();
        return parentType != null ? parentType.Identifier.Text : "(Global)";
    }

    #endregion

    #region Skeleton Export Command

    private static int RunSkeleton(string[] args)
    {
        string srcDir = args.Length > 0 ? args[0] : "GRC2";
        string outDir = args.Length > 1 ? args[1] : "GRC2-outline";
        string pattern = "*.cs";

        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--pattern" && i + 1 < args.Length)
                pattern = args[++i];
        }

        return ExportSkeleton(srcDir, outDir, pattern);
    }

    private static int ExportSkeleton(string srcDir, string outDir, string pattern)
    {
        var files = GetFiles(srcDir, pattern);
        if (files.Count == 0)
        {
            Console.Error.WriteLine($"No C# files found in: {srcDir}");
            return 1;
        }

        string fullSrc = Path.GetFullPath(srcDir);
        string fullOut = Path.GetFullPath(outDir);

        if (Directory.Exists(fullOut))
        {
            Directory.Delete(fullOut, true);
        }
        Directory.CreateDirectory(fullOut);

        Console.WriteLine($"[Roslyn AST] Exporting C# skeleton outline from '{srcDir}' to '{outDir}' ({files.Count} files)...");

        var rewriter = new SkeletonSyntaxRewriter();
        int processedCount = 0;
        long totalOriginalBytes = 0;
        long totalSkeletonBytes = 0;

        var summarySb = new StringBuilder();
        summarySb.AppendLine("# GRC2 Roslyn AST AI Skeleton Outline");
        summarySb.AppendLine($"Generated on: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        summarySb.AppendLine($"Source: `{srcDir}` ({files.Count} files)");
        summarySb.AppendLine($"Output Directory: `{outDir}`");
        summarySb.AppendLine();
        summarySb.AppendLine("## File Index & Line Comparison");

        foreach (var file in files)
        {
            string rel = Path.GetRelativePath(fullSrc, file);
            string targetPath = Path.Combine(fullOut, rel);
            string? targetFolder = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetFolder) && !Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }

            string sourceText = File.ReadAllText(file, Encoding.UTF8);
            totalOriginalBytes += Encoding.UTF8.GetByteCount(sourceText);

            var tree = CSharpSyntaxTree.ParseText(sourceText);
            var root = tree.GetRoot();
            var transformed = rewriter.Visit(root);
            var normalized = transformed.NormalizeWhitespace();

            string skeletonCode = normalized.ToFullString();
            File.WriteAllText(targetPath, skeletonCode, Encoding.UTF8);
            totalSkeletonBytes += Encoding.UTF8.GetByteCount(skeletonCode);

            int origLines = sourceText.Split('\n').Length;
            int skelLines = skeletonCode.Split('\n').Length;
            string relForward = rel.Replace('\\', '/');
            summarySb.AppendLine($"- [`{relForward}`]({relForward}) : {origLines}L -> {skelLines}L");

            processedCount++;
        }

        double compressionRate = totalOriginalBytes > 0 
            ? (1.0 - (double)totalSkeletonBytes / totalOriginalBytes) * 100.0 
            : 0;

        summarySb.AppendLine();
        summarySb.AppendLine("## Compression Summary");
        summarySb.AppendLine($"- Total files: {processedCount}");
        summarySb.AppendLine($"- Original bytes: {totalOriginalBytes:N0} bytes");
        summarySb.AppendLine($"- Skeleton bytes: {totalSkeletonBytes:N0} bytes");
        summarySb.AppendLine($"- Token reduction: {compressionRate:F1}%");

        File.WriteAllText(Path.Combine(fullOut, "README.md"), summarySb.ToString(), Encoding.UTF8);

        Console.WriteLine($"[Success] Exported {processedCount} skeleton files to '{outDir}'.");
        Console.WriteLine($"Original size: {totalOriginalBytes:N0} bytes -> Skeleton size: {totalSkeletonBytes:N0} bytes (Token savings: {compressionRate:F1}%)");
        return 0;
    }

    private sealed class SkeletonSyntaxRewriter : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            var updated = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!;
            if (updated.Body == null && updated.ExpressionBody == null)
                return updated;

            bool isVoid = updated.ReturnType.ToString().Trim() == "void";
            var emptyBody = isVoid
                ? SyntaxFactory.Block()
                : SyntaxFactory.Block(SyntaxFactory.ParseStatement("throw null;"));

            return updated
                .WithExpressionBody(null)
                .WithSemicolonToken(default)
                .WithBody(emptyBody);
        }

        public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            var updated = (ConstructorDeclarationSyntax)base.VisitConstructorDeclaration(node)!;
            if (updated.Body == null && updated.ExpressionBody == null)
                return updated;

            return updated
                .WithExpressionBody(null)
                .WithSemicolonToken(default)
                .WithBody(SyntaxFactory.Block());
        }

        public override SyntaxNode? VisitDestructorDeclaration(DestructorDeclarationSyntax node)
        {
            var updated = (DestructorDeclarationSyntax)base.VisitDestructorDeclaration(node)!;
            return updated
                .WithExpressionBody(null)
                .WithSemicolonToken(default)
                .WithBody(SyntaxFactory.Block());
        }

        public override SyntaxNode? VisitOperatorDeclaration(OperatorDeclarationSyntax node)
        {
            var updated = (OperatorDeclarationSyntax)base.VisitOperatorDeclaration(node)!;
            return updated
                .WithExpressionBody(null)
                .WithSemicolonToken(default)
                .WithBody(SyntaxFactory.Block(SyntaxFactory.ParseStatement("throw null;")));
        }

        public override SyntaxNode? VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node)
        {
            var updated = (ConversionOperatorDeclarationSyntax)base.VisitConversionOperatorDeclaration(node)!;
            return updated
                .WithExpressionBody(null)
                .WithSemicolonToken(default)
                .WithBody(SyntaxFactory.Block(SyntaxFactory.ParseStatement("throw null;")));
        }

        public override SyntaxNode? VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            var updated = (FieldDeclarationSyntax)base.VisitFieldDeclaration(node)!;

            // const 필드는 컴파일/시그니처 유지를 위해 리터럴 초기화 유지
            if (updated.Modifiers.Any(SyntaxKind.ConstKeyword))
                return updated;

            // 일반 필드의 초기화 식(= 람다, 익명 델리게이트, 객체 생성 등) 제거
            var strippedVariables = updated.Declaration.Variables.Select(v => v.WithInitializer(null));
            return updated.WithDeclaration(
                updated.Declaration.WithVariables(SyntaxFactory.SeparatedList(strippedVariables))
            );
        }

        public override SyntaxNode? VisitEventFieldDeclaration(EventFieldDeclarationSyntax node)
        {
            var updated = (EventFieldDeclarationSyntax)base.VisitEventFieldDeclaration(node)!;
            var strippedVariables = updated.Declaration.Variables.Select(v => v.WithInitializer(null));
            return updated.WithDeclaration(
                updated.Declaration.WithVariables(SyntaxFactory.SeparatedList(strippedVariables))
            );
        }

        public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            var updated = (PropertyDeclarationSyntax)base.VisitPropertyDeclaration(node)!;

            if (updated.Initializer != null)
            {
                updated = updated.WithInitializer(null);
                if (updated.AccessorList != null)
                {
                    updated = updated.WithSemicolonToken(default);
                }
            }

            if (updated.ExpressionBody != null)
            {
                var getter = SyntaxFactory.AccessorDeclaration(
                    SyntaxKind.GetAccessorDeclaration,
                    SyntaxFactory.Block(SyntaxFactory.ParseStatement("throw null;")));

                return updated
                    .WithExpressionBody(null)
                    .WithSemicolonToken(default)
                    .WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.SingletonList(getter)));
            }

            if (updated.AccessorList != null)
            {
                var accessors = updated.AccessorList.Accessors.Select(a =>
                {
                    if (a.Body != null || a.ExpressionBody != null)
                    {
                        if (a.Kind() == SyntaxKind.GetAccessorDeclaration)
                        {
                            return a.WithExpressionBody(null)
                                    .WithBody(SyntaxFactory.Block(SyntaxFactory.ParseStatement("throw null;")))
                                    .WithSemicolonToken(default);
                        }
                        else
                        {
                            return a.WithExpressionBody(null)
                                    .WithBody(SyntaxFactory.Block())
                                    .WithSemicolonToken(default);
                        }
                    }
                    return a;
                });

                return updated.WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.List(accessors)));
            }

            return updated;
        }

        public override SyntaxNode? VisitIndexerDeclaration(IndexerDeclarationSyntax node)
        {
            var updated = (IndexerDeclarationSyntax)base.VisitIndexerDeclaration(node)!;

            if (updated.ExpressionBody != null)
            {
                var getter = SyntaxFactory.AccessorDeclaration(
                    SyntaxKind.GetAccessorDeclaration,
                    SyntaxFactory.Block(SyntaxFactory.ParseStatement("throw null;")));

                return updated
                    .WithExpressionBody(null)
                    .WithSemicolonToken(default)
                    .WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.SingletonList(getter)));
            }

            if (updated.AccessorList != null)
            {
                var accessors = updated.AccessorList.Accessors.Select(a =>
                {
                    if (a.Body != null || a.ExpressionBody != null)
                    {
                        if (a.Kind() == SyntaxKind.GetAccessorDeclaration)
                        {
                            return a.WithExpressionBody(null)
                                    .WithBody(SyntaxFactory.Block(SyntaxFactory.ParseStatement("throw null;")))
                                    .WithSemicolonToken(default);
                        }
                        else
                        {
                            return a.WithExpressionBody(null)
                                    .WithBody(SyntaxFactory.Block())
                                    .WithSemicolonToken(default);
                        }
                    }
                    return a;
                });

                return updated.WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.List(accessors)));
            }

            return updated;
        }
    }

    #endregion
}
