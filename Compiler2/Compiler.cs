namespace Compiler;

using System;
using System.Text;
using System.Text.RegularExpressions;

public class Compiler
{
  public string Compile(string md)
  {
    return Gen(Parse(Tokenize(md)));
  }

  public List<Lexer.Token> Tokenize(string md)
  {
    return new Lexer(md).Tokenize();
  }

  public Parser.ASTRootNode Parse(List<Lexer.Token> tks)
  {
    return new Parser(tks).Parse();
  }

  public string Gen(Parser.ASTRootNode ast)
  {
    return new CodeGen(ast).Gen();
  }

  public static void Main(string[] args)
  {

  }
}

public class Lexer
{
  private string md;
  private List<Token> tks;

  int LIST_INDENT_SIZE = 2;

  public abstract record Token;
  public record HeaderToken(int size) : Token;
  public record TextToken(string text, bool bold, bool italic) : Token;
  public record ListItemToken(int indent, bool ordered, int digit) : Token;
  public record CodeBlockToken(string lang, string code) : Token;
  public record CodeInlineToken(string lang, string code) : Token;
  public record BlockQuoteToken(int indent) : Token;
  public record ImageToken(string alt, string src) : Token;
  public record LinkToken(string text, string href) : Token;
  public record HorizontalRuleToken() : Token;
  public record NewLineToken() : Token;

  public Lexer(string md)
  {
    this.md = md;
    this.tks = new List<Token>();
  }

  public List<Token> Tokenize()
  {
    while (!string.IsNullOrEmpty(this.md))
    {
      if (TryTokenizeHeader())
      {
        continue;
      }

      if (TryTokenizeCodeBlock())
      {
        continue;
      }

      if (TryTokenizeBlockQuote())
      {
        continue;
      }

      if (TryTokenizeHorizontalRule())
      {
        continue;
      }

      if (TryTokenizeList())
      {
        continue;
      }

      if (TryTokenizeHeaderAlt())
      {
        continue;
      }

      if (TryTokenizeNewLine())
      {
        continue;
      }

      TokenizeCurrentLine();
    }

    if (this.tks.Count > 0 && !(this.tks[^1].GetType() == typeof(NewLineToken)))
    {
      this.tks.Add(new NewLineToken());
    }

    return this.tks;
  }

  private static readonly Regex HeaderRegex = new Regex(@"\A(######|#####|####|###|##|#) ", RegexOptions.Compiled);

  private bool TryTokenizeHeader()
  {
    Match match = HeaderRegex.Match(this.md);
    if (!match.Success)
    {
      return false;
    }

    int hSize = match.Groups[1].Value.Length;
    this.tks.Add(new HeaderToken(hSize));
    SetMarkdownAt(hSize + 1); // header + space
    TokenizeCurrentLine();
    this.tks.Add(new HorizontalRuleToken());
    this.tks.Add(new NewLineToken());

    return true;
  }

  private static readonly Regex CodeBlockRegex = new Regex(@"\A```(.*?) *$", RegexOptions.Compiled);

  private bool TryTokenizeCodeBlock()
  {
    Match match = CodeBlockRegex.Match(this.md);
    if (!match.Success)
    {
      return false;
    }

    string lang = match.Groups[1] == null ? "" : match.Groups[1].Value;

    int codeStart = lang.Length;
    while (true)
    {
      codeStart++;
      if (codeStart >= this.md.Length)
      { // no ending to code block
        return false;
      }
      if (this.md[codeStart] == '\n')
      {
        codeStart++;
        break;
      }
    }

    int codeEnd = codeStart;
    char tick = '`';
    while (true)
    {
      if (codeEnd + 2 >= this.md.Length)
      { // no ending to code block
        return false;
      }
      if (this.md[codeEnd] == tick && this.md[codeEnd + 1] == tick && this.md[codeEnd + 2] == tick)
      {
        codeEnd--;
        break;
      }
      codeEnd++;
    }

    // make sure we haven't deleted from this.md until this point, since we need to
    // ensure there is an ending block. If there is no ending block, we would have
    // returned false somewhere above, and we can try tokenizing a different token.

    string code = this.md.Substring(codeStart, codeEnd + 1);
    SetMarkdownAt(codeEnd + 1 + 3); // 3 = ```
    this.tks.Add(new CodeBlockToken(lang, code));
    this.tks.Add(new NewLineToken());

    return true;
  }

  private static readonly Regex BlockQuoteRegex = new Regex(@"\A(>(?: >)* ?)", RegexOptions.Compiled);

  private bool TryTokenizeBlockQuote()
  {
    Match match = BlockQuoteRegex.Match(this.md);
    if (!match.Success)
    {
      return false;
    }

    int indent = Array.FindAll(match.Groups[1].Value.ToCharArray(), (char c) => c == '>').Length;
    this.tks.Add(new BlockQuoteToken(indent));
    SetMarkdownAt(match.Groups[1].Value.Length);
    TokenizeCurrentLine();

    return true;
  }

  private static readonly Regex HorizontalRuleRegex = new Regex(@"\A(\*{3,}[\* ]*|-{3,}[- ]*)$", RegexOptions.Compiled);

  private bool TryTokenizeHorizontalRule()
  {
    Match match = HorizontalRuleRegex.Match(this.md);
    if (!match.Success)
    {
      return false;
    }

    this.tks.Add(new HorizontalRuleToken());
    this.tks.Add(new NewLineToken());
    SetMarkdownAt(match.Groups[1].Value.Length + 1); // 1 for newl

    return true;
  }

  private static readonly Regex ListRegex = new Regex(@"\A *(([0-9]\.)|(\*|-)) ", RegexOptions.Compiled);

  private bool TryTokenizeList()
  {
    Match match = ListRegex.Match(this.md);
    if (!match.Success)
    {
      return false;
    }

    int spaces = 0;
    while (this.md[spaces] == ' ')
    {
      spaces += 1;
    }

    switch (this.md[spaces])
    {
      // un-ordered
      case '*':
      case '-':
        this.tks.Add(new ListItemToken(spaces / LIST_INDENT_SIZE, false, -1));
        SetMarkdownAt(spaces + 2); // 2 = */- + space
        break;

      // ordered
      default:
        // only support one digit for now
        int digit = Convert.ToInt32(new string(this.md[spaces], 1));
        this.tks.Add(new ListItemToken(spaces / LIST_INDENT_SIZE, true, digit));
        SetMarkdownAt(spaces + 3); // 3 = digit + period + space
        break;
    }

    TokenizeCurrentLine();

    return true;
  }

  private static readonly Regex HeaderAltRegex = new Regex(@"\A.+\n(=+|-+) *", RegexOptions.Compiled);

  private bool TryTokenizeHeaderAlt()
  {
    Match match = HeaderAltRegex.Match(this.md);
    if (!match.Success)
    {
      return false;
    }

    // search next line for header size
    int pointer = 0;
    while (this.md[pointer] != '\n')
    {
      pointer += 1;
    }
    char sizeChar = this.md[pointer + 2]; // newl + space
    switch (sizeChar)
    {
      case '=':
        this.tks.Add(new HeaderToken(1));
        break;
      case '-':
        this.tks.Add(new HeaderToken(2));
        break;
      default:
        throw new Exception($"Invalid char found for header alt: {sizeChar}");
    }

    TokenizeCurrentLine();
    DelCurrentLine(); // ---/=== line
    this.tks.Add(new HorizontalRuleToken());
    this.tks.Add(new NewLineToken());

    return true;
  }

  private static readonly Regex NewLineRegex = new Regex(@"\A\n", RegexOptions.Compiled);

  private bool TryTokenizeNewLine()
  {
    Match match = NewLineRegex.Match(this.md);
    if (!match.Success)
    {
      return false;
    }

    this.tks.Add(new NewLineToken());
    SetMarkdownAt(1);

    return true;
  }

  private static readonly Regex BoldAndItalicRegex = new Regex(@"\A(\*{3}[^\*]+?\*{3}|_{3}[^_]+?_{3})", RegexOptions.Compiled);
  private static readonly Regex BoldRegex = new Regex(@"\A(\*{2}[^\*]+?\*{2}|_{2}[^_]+?_{2})", RegexOptions.Compiled);
  private static readonly Regex ItalicRegex = new Regex(@"\A(\*[^\*]+?\*|_[^_]+?_)", RegexOptions.Compiled);
  private static readonly Regex ImageRegex = new Regex(@"\A!\[(.*)\]\((.*)\)", RegexOptions.Compiled);
  private static readonly Regex LinkRegex = new Regex(@"\A\[(.*)\]\((.*)\)", RegexOptions.Compiled);
  private static readonly Regex CodeInlineRegex = new Regex(@"\A`(.+?)`([a-z]*)", RegexOptions.Compiled);

  private void TokenizeCurrentLine()
  {
    if (string.IsNullOrEmpty(this.md))
    {
      return;
    }
    if (this.md[0] == '\n')
    { // already at the end of the current line
      this.tks.Add(new NewLineToken());
      SetMarkdownAt(1);
      return;
    }

    // find current line
    string? line = null;
    int lineEnd = 0;
    while (true)
    {
      lineEnd++;
      if (lineEnd == this.md.Length)
      { // EOF
        break;
      }
      if (this.md[lineEnd] == '\n')
      {
        lineEnd++;
        break;
      }
    }
    if (lineEnd == this.md.Length)
    {
      line = this.md;
    }
    else
    {
      line = this.md.Substring(0, lineEnd);
    }
    SetMarkdownAt(line.Length);

    StringBuilder currStr = new StringBuilder();
    void CurrAdd()
    {
      if (currStr.Length > 0)
      {
        this.tks.Add(new TextToken(currStr.ToString(), false, false));
        currStr.Clear();
      }
    }

    while (!string.IsNullOrEmpty(line))
    {
      // == bold and italic ==
      Match match = BoldAndItalicRegex.Match(line);
      if (match.Success)
      {
        CurrAdd();

        this.tks.Add(new TextToken(match.Groups[1].Value.Replace("*", "").Replace("_", ""), true, true));
        line = line.Substring(match.Groups[1].Value.Length);

        continue;
      }

      // == bold ==
      match = BoldRegex.Match(line);
      if (match.Success)
      {
        CurrAdd();

        this.tks.Add(new TextToken(match.Groups[1].Value.Replace("*", "").Replace("_", ""), true, false));
        line = line.Substring(match.Groups[1].Value.Length);

        continue;
      }

      // == italic ==
      match = ItalicRegex.Match(line);
      if (match.Success)
      {
        CurrAdd();

        this.tks.Add(new TextToken(match.Groups[1].Value.Replace("*", "").Replace("_", ""), false, true));
        line = line.Substring(match.Groups[1].Value.Length);

        continue;
      }

      // == image ==
      match = ImageRegex.Match(line);
      if (match.Success)
      {
        CurrAdd();

        this.tks.Add(new ImageToken(match.Groups[1].Value, match.Groups[2].Value));
        line = line.Substring(match.Groups[1].Value.Length + match.Groups[2].Value.Length + 5); // 5 = ![]()

        continue;
      }

      // == link ==
      match = LinkRegex.Match(line);
      if (match.Success)
      {
        CurrAdd();

        this.tks.Add(new LinkToken(match.Groups[1].Value, match.Groups[2].Value));
        line = line.Substring(match.Groups[1].Value.Length + match.Groups[2].Value.Length + 4); // 4 = ![]()

        continue;
      }

      // == code ==
      match = CodeInlineRegex.Match(line);
      if (match.Success)
      {
        CurrAdd();

        string code = match.Groups[1].Value;
        string lang = match.Groups[2] == null ? "" : match.Groups[2].Value;

        this.tks.Add(new CodeInlineToken(lang, code));
        line = line.Substring(code.Length + lang.Length + 2); // 2 = ``

        continue;
      }

      // == new line ==
      if (line[0] == '\n')
      {
        CurrAdd();

        this.tks.Add(new NewLineToken());

        break;
      }

      currStr.Append(line[0]);
      line = line.Substring(1);
    }

    CurrAdd();
  }

  private void DelCurrentLine()
  {
    int pointer = 0;
    while (pointer < this.md.Length)
    {
      pointer++;
      if (this.md[pointer] == '\n')
      {
        SetMarkdownAt(pointer + 1);
        break;
      }
    }
  }

  private void SetMarkdownAt(int index)
  {
    this.md = index < this.md.Length ? this.md.Substring(index) : "";
  }
}

public class Parser
{
  private List<Lexer.Token> tks;
  private int tksStart;
  private ASTRootNode root;

  public abstract record ASTNode;
  public record ASTRootNode(List<ASTNode> children) : ASTNode;
  public record ASTHeaderNode(int size, List<ASTNode> children) : ASTNode;
  public record ASTCodeBlockNode(string lang, string code) : ASTNode;
  public record ASTCodeInlineNode(string lang, string code) : ASTNode;
  public record ASTQuoteNode(List<ASTNode> children) : ASTNode;
  public record ASTQuoteItemNode(List<ASTNode> children) : ASTNode;
  public record ASTParagraphNode(List<ASTNode> children) : ASTNode;
  public record ASTTextNode(string text, bool bold, bool italic) : ASTNode;
  public record ASTHorizontalRuleNode() : ASTNode;
  public record ASTImageNode(string alt, string src) : ASTNode;
  public record ASTLinkNode(string text, string href) : ASTNode;
  public record ASTListNode(bool ordered, List<ASTNode> children) : ASTNode;
  public record ASTListItemNode(List<ASTNode> children) : ASTNode;

  public Parser(List<Lexer.Token> tks)
  {
    this.tks = tks;
    this.tksStart = 0;
    this.root = new ASTRootNode(new List<ASTNode>());
  }

  public ASTRootNode Parse()
  {
    while (this.tksStart < this.tks.Count)
    {
      if (Peek(typeof(Lexer.HeaderToken)))
      {
        ParseHeader();
      }
      else if (Peek(typeof(Lexer.CodeBlockToken)))
      {
        ParseCodeBlock();
      }
      else if (Peek(typeof(Lexer.BlockQuoteToken)))
      {
        ParseBlockQuote();
      }
      else if (Peek(typeof(Lexer.HorizontalRuleToken)))
      {
        ParseHorizontalRule();
      }
      else if (Peek(typeof(Lexer.ListItemToken)))
      {
        ParseList();
      }
      else if (Peek(typeof(Lexer.ImageToken)))
      {
        ParseImage();
      }
      else if (PeekAny(new Type[] { typeof(Lexer.TextToken), typeof(Lexer.CodeInlineToken), typeof(Lexer.LinkToken) }))
      {
        ParseParagraph();
      }
      else if (Peek(typeof(Lexer.NewLineToken)))
      {
        Consume<Lexer.NewLineToken>();
      }
      else
      {
        throw new Exception($"Unable to parse tokens:\n{this.tks}");
      }
    }

    return this.root;
  }

  private void ParseHeader()
  {
    Lexer.HeaderToken token = Consume<Lexer.HeaderToken>();
    this.root.children.Add(new ASTHeaderNode(token.size, ParseInline()));
  }

  private void ParseCodeBlock()
  {
    Lexer.CodeBlockToken token = Consume<Lexer.CodeBlockToken>();
    Consume<Lexer.NewLineToken>();
    this.root.children.Add(new ASTCodeBlockNode(token.lang, token.code));
  }

  private void ParseBlockQuote()
  {
    int rootIndent = Consume<Lexer.BlockQuoteToken>().indent;
    ASTQuoteNode rootBlock = new ASTQuoteNode(new List<ASTNode> { ParseQuoteItem() });
    Dictionary<int, ASTQuoteNode> blockIndentMap = new Dictionary<int, ASTQuoteNode> { { rootIndent, rootBlock } };

    while (Peek(typeof(Lexer.BlockQuoteToken)))
    {
      Lexer.BlockQuoteToken block = Consume<Lexer.BlockQuoteToken>();
      if (Peek(typeof(Lexer.NewLineToken)))
      {
        Consume<Lexer.NewLineToken>();
        continue;
      }

      blockIndentMap.TryGetValue(block.indent, out ASTQuoteNode blockNode);
      if (blockNode == null)
      {
        blockNode = new ASTQuoteNode(new List<ASTNode> { ParseQuoteItem() });
        blockIndentMap[block.indent] = blockNode;
        ASTQuoteNode blockParent = blockIndentMap.ContainsKey(block.indent - 1) ? blockIndentMap[block.indent - 1] : rootBlock;
        blockParent.children.Add(blockNode);
      }
      else
      {
        blockNode.children.Add(ParseQuoteItem());
      }
    }

    this.root.children.Add(rootBlock);
  }

  private ASTQuoteItemNode ParseQuoteItem()
  {
    return new ASTQuoteItemNode(ParseInlineBlockQuote());
  }

  private void ParseHorizontalRule()
  {
    Consume<Lexer.HorizontalRuleToken>();
    Consume<Lexer.NewLineToken>();
    this.root.children.Add(new ASTHorizontalRuleNode());
  }

  private record ListStackItem(Parser.ASTListNode node, int indent);

  private void ParseList()
  {
    // create root
    ASTListNode rootList = new ASTListNode(Consume<Lexer.ListItemToken>().ordered, new List<Parser.ASTNode>());
    rootList.children.Add(new ASTListItemNode(ParseInline()));

    Stack<ListStackItem> listStack = new Stack<ListStackItem>();
    listStack.Push(new ListStackItem(rootList, 0));

    while (Peek(typeof(Lexer.ListItemToken)))
    {
      Lexer.ListItemToken currToken = Consume<Lexer.ListItemToken>();
      int currIndent = Math.Min(listStack.Peek().indent + 1, currToken.indent); // only allow 1 additional level at a time
      int lastIndent = listStack.Peek().indent;
      if (currIndent > lastIndent)
      { // deeper indentation
        // create new node
        ASTListNode node = new ASTListNode(currToken.ordered, new List<Parser.ASTNode> { new ASTListItemNode(ParseInline()) });

        // append to last child of top (of stack) node
        List<ASTNode> topNodeChildren = listStack.Peek().node.children;
        if (topNodeChildren[^1].GetType() == typeof(ASTListItemNode))
        {
          ((ASTListItemNode)topNodeChildren[^1]).children.Add(node);
        }
        else
        {
          throw new Exception($"Unexpected top node last child: {topNodeChildren}");
        }

        // add to stack
        listStack.Push(new ListStackItem(node, currIndent));
      }
      else if (currIndent < lastIndent)
      { // lost indentation
        // pop from stack until we find current level
        while (listStack.Peek().indent > currIndent)
        {
          listStack.Pop();
        }
        listStack.Peek().node.children.Add(new ASTListItemNode(ParseInline()));
      }
      else
      { // same indentation
        listStack.Peek().node.children.Add(new ASTListItemNode(ParseInline()));
      }
    }

    this.root.children.Add(rootList);
  }

  private void ParseImage()
  {
    Lexer.ImageToken token = Consume<Lexer.ImageToken>();
    Consume<Lexer.NewLineToken>();
    this.root.children.Add(new ASTImageNode(token.alt, token.src));
  }

  private void ParseParagraph()
  {
    this.root.children.Add(new ASTParagraphNode(ParseInline()));
  }

  private static readonly Type[] INLINE_TOKENS = new Type[] {
    typeof(Lexer.TextToken),
    typeof(Lexer.CodeInlineToken),
    typeof(Lexer.LinkToken)
  };

  private List<ASTNode> ParseInline()
  {
    List<ASTNode> nodes = new List<ASTNode>();

    while (PeekAny(INLINE_TOKENS) || (Peek(typeof(Lexer.NewLineToken)) && PeekAny(INLINE_TOKENS, 2)))
    {
      if (Peek(typeof(Lexer.NewLineToken)))
      {
        Consume<Lexer.NewLineToken>();
        nodes.Add(new ASTTextNode(" ", false, false));
      }

      nodes.Add(ParseInlineSingle());
    }
    Consume<Lexer.NewLineToken>();

    return nodes;
  }

  private List<ASTNode> ParseInlineBlockQuote()
  {
    List<ASTNode> nodes = new List<ASTNode>();

    while (PeekAny(INLINE_TOKENS) || (Peek(typeof(Lexer.NewLineToken)) && Peek(typeof(Lexer.BlockQuoteToken), 2) && PeekAny(INLINE_TOKENS, 3)))
    {
      if (Peek(typeof(Lexer.NewLineToken)))
      {
        Consume<Lexer.NewLineToken>();
        Consume<Lexer.BlockQuoteToken>();
        nodes.Add(new ASTTextNode(" ", false, false));
      }

      nodes.Add(ParseInlineSingle());
    }
    Consume<Lexer.NewLineToken>();

    return nodes;
  }

  private ASTNode ParseInlineSingle()
  {
    if (Peek(typeof(Lexer.TextToken)))
    {
      Lexer.TextToken token = Consume<Lexer.TextToken>();
      return new ASTTextNode(token.text, token.bold, token.italic);
    }
    else if (Peek(typeof(Lexer.CodeInlineToken)))
    {
      Lexer.CodeInlineToken token = Consume<Lexer.CodeInlineToken>();
      return new ASTCodeInlineNode(token.lang, token.code);
    }
    else if (Peek(typeof(Lexer.LinkToken)))
    {
      Lexer.LinkToken token = Consume<Lexer.LinkToken>();
      return new ASTLinkNode(token.text, token.href);
    }
    else
    {
      throw new Exception($"Unexpected next token:\n{this.tks}");
    }
  }

  private bool Peek(Type tokenType, int depth = 1)
  {
    int index = this.tksStart + depth - 1;
    if (index >= this.tks.Count)
    {
      return false;
    }
    return this.tks[index].GetType() == tokenType;
  }

  private bool PeekAny(Type[] tokenTypes, int depth = 1)
  {
    foreach (Type tokenType in tokenTypes)
    {
      if (Peek(tokenType, depth))
      {
        return true;
      }
    }
    return false;
  }

  private T Consume<T>() where T : Lexer.Token
  {
    if (this.tksStart == this.tks.Count)
    {
      throw new Exception($"Expected to find token type {typeof(T).Name} but did not find a token");
    }

    Lexer.Token token = this.tks[this.tksStart];
    this.tksStart += 1;
    if (token is T typedToken)
    {
      return typedToken;
    }

    throw new Exception($"Expected to find token type {typeof(T).Name} but did find {token.GetType()}");
  }
}

public class CodeGen
{
  private Parser.ASTRootNode ast;
  private StringBuilder html;

  public CodeGen(Parser.ASTRootNode ast)
  {
    this.ast = ast;
    this.html = new StringBuilder();
  }

  public string Gen()
  {
    foreach (Parser.ASTNode node in this.ast.children)
    {
      Type type = node.GetType();
      if (type == typeof(Parser.ASTHeaderNode))
      {
        this.html.Append(GenHeader((Parser.ASTHeaderNode)node));
      }
      else if (type == typeof(Parser.ASTCodeBlockNode))
      {
        this.html.Append(GenCodeBlock((Parser.ASTCodeBlockNode)node));
      }
      else if (type == typeof(Parser.ASTQuoteNode))
      {
        this.html.Append(GenQuoteBlock((Parser.ASTQuoteNode)node));
      }
      else if (type == typeof(Parser.ASTListNode))
      {
        this.html.Append(GenList((Parser.ASTListNode)node));
      }
      else if (type == typeof(Parser.ASTHorizontalRuleNode))
      {
        this.html.Append(GenHorizontalRule((Parser.ASTHorizontalRuleNode)node));
      }
      else if (type == typeof(Parser.ASTImageNode))
      {
        this.html.Append(GenImage((Parser.ASTImageNode)node));
      }
      else if (type == typeof(Parser.ASTLinkNode))
      {
        this.html.Append(GenLink((Parser.ASTLinkNode)node));
      }
      else if (type == typeof(Parser.ASTCodeInlineNode))
      {
        this.html.Append(GenCodeInline((Parser.ASTCodeInlineNode)node));
      }
      else if (type == typeof(Parser.ASTParagraphNode))
      {
        this.html.Append(GenParagraph((Parser.ASTParagraphNode)node));
      }
      else
      {
        throw new Exception("Invalid node: " + node);
      }
    }

    return this.html.ToString();
  }

  private string GenHeader(Parser.ASTHeaderNode node)
  {
    return $"<h{node.size}>{GenLine(node.children)}</h{node.size}>";
  }

  private string GenCodeBlock(Parser.ASTCodeBlockNode node)
  {
    return $"<pre><code class=\"{EscapeHtml(node.lang)}\">{node.code}</code></pre>";
  }

  private string GenQuoteBlock(Parser.ASTQuoteNode node)
  {
    StringBuilder html = new StringBuilder("<blockquote>");

    foreach (Parser.ASTNode child in node.children)
    {
      Type type = child.GetType();
      if (type == typeof(Parser.ASTQuoteNode))
      {
        html.Append(GenQuoteBlock((Parser.ASTQuoteNode)child));
      }
      else if (type == typeof(Parser.ASTQuoteItemNode))
      {
        html.Append($"<p>{GenLine(((Parser.ASTQuoteItemNode)child).children)}</p>");
      }
      else
      {
        throw new Exception($"Invalid child node: {child}");
      }
    }

    html.Append("</blockquote>");
    return html.ToString();
  }

  private string GenList(Parser.ASTListNode node)
  {
    StringBuilder html = new StringBuilder(node.ordered ? "<ol>" : "<ul>");

    foreach (Parser.ASTNode child in node.children)
    {
      html.Append("<li>");
      if (!(child.GetType() == typeof(Parser.ASTListItemNode)))
      {
        throw new Exception($"Invalid child of list node: {child}");
      }
      foreach (Parser.ASTNode innerChild in ((Parser.ASTListItemNode)child).children)
      {
        if (innerChild.GetType() == typeof(Parser.ASTListNode))
        {
          html.Append(GenList((Parser.ASTListNode)innerChild));
        }
        else
        {
          html.Append(GenLine(new List<Parser.ASTNode> { innerChild }));
        }
      }
      html.Append("</li>");
    }

    html.Append(node.ordered ? "</ol>" : "</ul>");
    return html.ToString();
  }

  private string GenHorizontalRule(Parser.ASTHorizontalRuleNode node)
  {
    return "<hr>";
  }

  private string GenImage(Parser.ASTImageNode node)
  {
    return $"<img alt=\"{EscapeHtml(node.alt)}\" src=\"{EscapeHtml(node.src)}\">";
  }

  private string GenLink(Parser.ASTLinkNode node)
  {
    return $"<a href=\"{EscapeHtml(node.href)}\">{EscapeHtml(node.text)}</a>";
  }

  private string GenCodeInline(Parser.ASTCodeInlineNode node)
  {
    return $"<code class=\"{EscapeHtml(node.lang)}\">{node.code}</code>";
  }

  private string GenParagraph(Parser.ASTParagraphNode node)
  {
    return $"<p>{GenLine(node.children)}</p>";
  }

  private string GenLine(List<Parser.ASTNode> nodes)
  {
    StringBuilder html = new StringBuilder();

    foreach (Parser.ASTNode node in nodes)
    {
      Type type = node.GetType();
      if (type == typeof(Parser.ASTLinkNode))
      {
        html.Append(GenLink((Parser.ASTLinkNode)node));
      }
      else if (type == typeof(Parser.ASTCodeInlineNode))
      {
        html.Append(GenCodeInline((Parser.ASTCodeInlineNode)node));
      }
      else if (type == typeof(Parser.ASTTextNode))
      {
        html.Append(GenText((Parser.ASTTextNode)node));
      }
      else
      {
        throw new Exception($"Invalid node: {node}");
      }
    }

    return html.ToString();
  }

  private string GenText(Parser.ASTTextNode node)
  {
    StringBuilder html = new StringBuilder(node.text);
    if (node.bold)
    {
      html.Insert(0, "<b>");
      html.Insert(html.Length, "</b>");
    }
    if (node.italic)
    {
      html.Insert(0, "<i>");
      html.Insert(html.Length, "</i>");
    }
    return html.ToString();
  }

  private static Dictionary<char, string> ESCAPE_HTML_MAP = new Dictionary<char, string>{
    {'<', "&lt;"},
    {'>', "&gt;"},
    {'&', "&amp;"},
    {'"', "&quot;"}
  };

  private string EscapeHtml(string str)
  {
    StringBuilder? sb = null;

    for (int i = 0; i < str.Length; i++)
    {
      ESCAPE_HTML_MAP.TryGetValue(str[i], out string replacement);
      if (replacement != null)
      {
        if (sb == null)
        {
          sb = new StringBuilder(str.Length + 20); // extra capacity since string length will go up
          sb.Append(str, 0, i); // copy everything up to first match
        }
        sb.Append(replacement);
      }
      else if (sb != null)
      {
        sb.Append(str[i]);
      }
    }

    // if no replacements were needed, return original
    return sb == null ? str : sb.ToString();
  }
}