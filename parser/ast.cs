using sr_mml_lang.lexer;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;

namespace sr_mml_lang.parser;

using IEvalItem = IEvalItem<string?>;
using IEvalItemList = IEnumerable<IEvalItem<string?>>;
using IStringEvalItem = IEvalItem<string>;


[DebuggerDisplay("AST item: const")]
class ConstItem(IStringEvalItem nameItem, IStringEvalItem valueItem) : ActionASTItem() {
  public override void Eval(Current current) {
    string name = nameItem.EvalStr(current);
    string value = valueItem.EvalStr(current);

    PrintInfo(name, value);
    current.SetConst(name, value);
  }

  static void PrintInfo(string name, string value) {
    Debug.WriteLine($"Set constant '{name}' value to '{value}'");
  }
}


[DebuggerDisplay("AST item: preset")]
class PresetItem(IStringEvalItem nameItem, AST children, IEvalItemList argsList) : ActionASTItem() {
  public override void Eval(Current current) {
    string name = nameItem.EvalStr(current);
    var presetArgs = argsList.Select(arg => arg.EvalStr(current));

    PrintDefineInfo(name, presetArgs);
    current.SetFunc(name, args => {
      var evalArgs = args.Select(arg => arg.EvalStr(current));

      if (evalArgs.Last() != UseItem.UseFlag) {
        throw new InvalidUseException();
      }

      evalArgs = evalArgs.SkipLast(1);
      if (presetArgs.Count() > evalArgs.Count()) {
        throw new MissingArgsException(name, presetArgs.Count(), evalArgs.Count());
      }

      PrintCallInfo(name, evalArgs);
      current.Isolated(() => {
        for (int i = 0; i < presetArgs.Count(); ++i) {
          var arg = evalArgs.ElementAt(i)
            ?? throw new InvalidDefineArgException(name, i);
          var presetArg = presetArgs.ElementAt(i)
            ?? throw new InvalidCallArgException(name, i);

          current.SetConst(presetArg, arg);
        }

        children.Eval(current);
      });

      return null;
    });
  }

  static void PrintDefineInfo(string name, IEnumerable<string?> args) {
    Debug.WriteLine($"Define preset '{name}' with args '{string.Join(' ', args)}'");
  }

  static void PrintCallInfo(string name, IEnumerable<string?> args) {
    Debug.WriteLine($"Call preset '{name}' with args [{string.Join(", ", args)}]");
  }

  class InvalidUseException()
    : ParseException("Use preset without 'use' is unavailable");

  class MissingArgsException(string name, int expected, int found)
    : ParseException($"Missing arguments on calling '{name}'. Expected {expected} found {found}");

  class InvalidDefineArgException(string name, int index)
    : ParseException($"Calling function '{name}' argument {index} is null");

  class InvalidCallArgException(string name, int index)
    : ParseException($"Function '{name}' argument {index} is null");
}


[DebuggerDisplay("AST item: use preset")]
class UseItem(IStringEvalItem nameItem, IEvalItemList args) : ActionASTItem() {
  public static string UseFlag { get; } = new Random().NextInt64().ToString();

  public override void Eval(Current current) {
    string name = nameItem.EvalStr(current);

    PrintInfo(name);
    current.CallFunc(name, [.. args, new ValueItem.String(Token.NewString(UseFlag))]);
  }

  static void PrintInfo(string name) {
    Debug.WriteLine($"Use preset '{name}'");
  }
}


[DebuggerDisplay("AST item: remove")]
abstract class RemoveItem() : ActionASTItem() {
  [DebuggerDisplay("AST item: remove element")]
  public class Element(IStringEvalItem selectorItem) : RemoveItem() {
    public override void Eval(Current current) {
      string selector;
      XElement? element;

      if (current.Element is null) {
        PrintScopeError();
        return;
      }

      selector = selectorItem.EvalStr(current);
      element = current.Element.XPathSelectElement(selector);

      PrintInfo(selector);
      if (element is null) {
        PrintNotFound(selector);
      }

      element?.Remove();
    }

    static void PrintInfo(string selector) {
      Debug.WriteLine($"Remove element {selector}");
    }

    static void PrintNotFound(string selector) {
      Console.WriteLine($"Element with selector '{selector}' not found");
    }

    [DebuggerDisplay("ASTItem:RemoveAllElements selector: '{SelectorItem}'")]
    public class All(IStringEvalItem selectorItem) : RemoveItem() {
      public override void Eval(Current current) {
        string selector;
        IEnumerable<XElement> elements;

        if (current.Element is null) {
          PrintScopeError();
          return;
        }

        selector = selectorItem.EvalStr(current);
        PrintInfo(selector);

        elements = current.Element.XPathSelectElements(selector) ?? [];
        if (elements.Any()) {
          PrintNotFound(selector);
        }

        foreach (var element in elements.ToArray()) {
          element.Remove();
        }
      }

      static void PrintInfo(string selector) {
        Debug.WriteLine($"Remove all elements {selector}");
      }

      static void PrintNotFound(string selector) {
        Console.WriteLine($"Elements with selector '{selector}' not found");
      }
    }
  }

  [DebuggerDisplay("AST item: remove attribute")]
  public class Attribute(IStringEvalItem nameItem) : RemoveItem() {
    public override void Eval(Current current) {
      string name;

      if (current.Element is null) {
        PrintScopeError();
        return;
      }

      name = nameItem.EvalStr(current);
      PrintInfo(name);

      current.Element.Attribute(name)?.Remove();
    }

    static void PrintInfo(string name) {
      Debug.WriteLine($"Remove attribute {name}");
    }
  }

  static void PrintScopeError() {
    Console.Error.WriteLine("'remove' allow only in 'file'/'change'/'add'");
  }
}


[DebuggerDisplay("AST item: set attribute")]
class SetAttrItem(IStringEvalItem nameItem, IStringEvalItem valueItem) : ActionASTItem() {
  public override void Eval(Current current) {
    string name, value;

    if (current.Element is null) {
      PrintScopeError();
      return;
    }

    name = nameItem.EvalStr(current);
    value = valueItem.EvalStr(current);
    PrintInfo(name, value);

    SetAttribute(current.Element, name, value);
  }

  static void PrintInfo(string name, string value) {
    Debug.WriteLine($"Set element attribute {name} value {value}");
  }

  static void PrintScopeError() {
    Console.Error.WriteLine("'set' allow only in 'change'/'add'");
  }

  static void SetAttribute(XElement element, string name, string value) {
    if (element.Attribute(name) is null) {
      element.Add(new XAttribute(name, value));
    }
    else {
      element.SetAttributeValue(name, value);
    }
  }
}


[DebuggerDisplay("AST item: include")]
class IncludeItem(IStringEvalItem nameItem, Parser.FileReader readFile) : ActionASTItem() {
  public override void Eval(Current current) {
    string name = nameItem.EvalStr(current);
    string content = readFile(name)
      ?? throw new FileNotFoundException($"File '{name}.mml' not found");

    new Parser().Parse(content, readFile);
  }
}


[DebuggerDisplay("AST item: change file")]
class FileItem(IStringEvalItem pathItem, AST children) : ActionASTItem() {
  public override void Eval(Current current) {
    string path;
    XDocument xDoc;

    if (current.Xml is not null) {
      PrintError();
      return;
    }

    path = $"./files/temp/[media]/{pathItem.EvalStr(current)}.xml"
      .Replace("..", ".");

    PrintInfo(path);
    if (!File.Exists(path)) {
      PrintNotFound(path);
      return;
    }

    xDoc = ReadXML(path);
    current.ResetXML();
    current.WithXML(xDoc, children);
    WriteXML(path, xDoc);
  }

  static void PrintInfo(string path) {
    Debug.WriteLine($"Change file '{path}'");
  }

  static void PrintNotFound(string path) {
    Console.Error.WriteLine($"File '{path.Replace("./files/temp/", "")}' not found");
  }

  static void PrintError() {
    Console.Error.WriteLine("XML is null");
  }

  static XDocument ReadXML(string path) {
    string content = File.ReadAllText(path).Replace("region:", "region_");
    return XDocument.Parse($"<root>{content}</root>");
  }

  static void WriteXML(string path, XDocument xDoc) {
    var elements = xDoc.Root!.Elements();
    string xml = (elements.Count() == 1
      ? elements.First().ToString()
      : $"{elements.First()}\n{elements.ElementAt(1)}"
    ).Replace("region_", "region:");

    File.WriteAllText(path, xml);
  }
}


[DebuggerDisplay("AST item: add element")]
class AddElementItem(IStringEvalItem tagItem, AST children, IStringEvalItem? indexItem = null) : ActionASTItem() {
  public override void Eval(Current current) {
    string tag;
    int index;

    if (current.Element is null) {
      PrintScopeError();
      return;
    }

    tag = tagItem.EvalStr(current);
    index = (int)double.Parse(indexItem?.EvalStr(current) ?? "1");
    PrintInfo(tag, index);

    if (
      indexItem is not null && (
        index < 1 ||
        (index > 1 && current.Element.XPathSelectElement($"{tag}[{index - 1}]") is null) ||
        current.Element.XPathSelectElement($"{tag}[{index}]") is not null
      )
    ) {
      return;
    }

    current.WithElement(AddElement(tag, current.Element), children);
  }

  static void PrintScopeError() {
    Console.Error.WriteLine("'add' only allow in 'file'/'change'/'add'");
  }

  static void PrintInfo(string tag, int index) {
    Debug.WriteLine($"Add element '{tag}' at '{index}' and change it");
  }

  static XElement AddElement(string tag, XElement container) {
    XElement element = new(tag);

    container.Add(element);
    return element;
  }
}


[DebuggerDisplay("AST item: change element")]
class ChangeElementItem(IStringEvalItem selectorItem, AST children) : ActionASTItem() {
  public override void Eval(Current current) {
    string selector;
    XElement? element;

    if (current.Element is null) {
      PrintScopeError();
      return;
    }

    selector = selectorItem.EvalStr(current);
    PrintInfo(selector);

    element = current.Element.XPathSelectElement(selector);
    if (element is null) {
      PrintNotFound(selector);
      return;
    }

    current.WithElement(element, children);
  }

  static void PrintScopeError() {
    Console.Error.WriteLine("'change' allow only in 'file'/'change'/'add'");
  }

  static void PrintNotFound(string selector) {
    Console.WriteLine($"Element with selector '{selector}' not found");
  }

  static void PrintInfo(string selector) {
    Debug.WriteLine($"Change element '{selector}'");
  }


  [DebuggerDisplay("AST item: change all elements")]
  public class All(IStringEvalItem selectorItem, AST children) : ChangeElementItem(selectorItem, children) {
    IStringEvalItem SelectorItem { get; } = selectorItem;
    AST Children { get; } = children;

    public override void Eval(Current current) {
      string selector;
      IEnumerable<XElement> elements;

      if (current.Element is null) {
        PrintScopeError();
        return;
      }

      selector = SelectorItem.EvalStr(current);
      PrintInfo(selector);

      elements = current.Element.XPathSelectElements(selector) ?? [];
      if (!elements.Any()) {
        PrintNotFound(selector);
        return;
      }

      foreach (var element in elements) {
        current.WithElement(element, Children);
      }
    }

    new static void PrintInfo(string selector) {
      Debug.WriteLine($"Change all elements '{selector}'");
    }
  }

  [DebuggerDisplay("AST item: change or add element")]
  public class OrAdd(IStringEvalItem tagItem, AST children, IStringEvalItem? indexItem = null) : ChangeElementItem(tagItem, children) {
    IStringEvalItem TagItem { get; } = tagItem;
    AST Children { get; } = children;

    public override void Eval(Current current) {
      string tag;
      int index;

      if (current.Element is null) {
        PrintScopeError();
        return;
      }

      tag = TagItem.EvalStr(current);
      index = (int)double.Parse(indexItem?.EvalStr(current) ?? "1");
      PrintInfo(tag, index);

      if (index < 1) {
        throw new InvalidIndexException(index);
      }
      if (
        index > 1 &&
        current.Element.XPathSelectElement($"{tag}[{index - 1}]") is null
      ) {
        PrintPrevNotFound(tag);
        return;
      }

      current.WithElement(
        current.Element.XPathSelectElement($"{tag}[{index}]") ?? AddElement(tag, current.Element),
        Children
      );
    }

    static void PrintInfo(string tag, int index) {
      Debug.WriteLine($"Change or add element '{tag}' at '{index}'");
    }

    static void PrintPrevNotFound(string tag) {
      Console.Error.WriteLine($"Previous element '{tag}' not found");
    }

    class InvalidIndexException(int index)
      : ParseException($"Invalid index '{index}'");

    static XElement AddElement(string tag, XElement container) {
      XElement element = new(tag);

      container.Add(element);
      return element;
    }
  }
}


[DebuggerDisplay("AST item: value")]
partial class ValueItem(Token token) : ValueASTItem(), IStringEvalItem {
  public override string EvalStr(Current _) {
    return token.Value;
  }

  [DebuggerDisplay("AST item: number")]
  public class Number(Token token, bool isNegative = false) : ValueItem(token), INumberItem {
    public override string EvalStr(Current current) {
      string value = base.EvalStr(current);
      return isNegative ? $"-{value}" : value;
    }

    [DebuggerDisplay("AST item: integer")]
    public class Int(Token token, bool isNegative = false) : Number(token) {
      string Value { get; } = token.Value;

      public int EvaluateInt() {
        return (int)double.Parse(Value);
      }

      public override string EvalStr(Current _) {
        int value = EvaluateInt();

        if (isNegative) {
          value = -value;
        }

        return value.ToString();
      }
    }
  }

  [DebuggerDisplay("AST item: string")]
  public partial class String(Token token) : ValueItem(token), IStringItem {
    string Value { get; } = token.Value;

    public override string EvalStr(Current current) {
      string value = Value;

      foreach (Match match in StringTemplate().Matches(Value).Cast<Match>()) {
        string name = match.Groups[1].Value;
        string varValue = current.GetConst(name)
          ?? throw new NullConstValueException(name);

        value = value.Replace($"{{{name}}}", varValue);
      }

      return value;
    }

    class NullConstValueException(string name)
      : ParseException($"Const '{name}' value is null");

    [GeneratedRegex(@"{(\w+)}")]
    private static partial Regex StringTemplate();
  }

  [DebuggerDisplay("AST item: boolean")]
  public partial class Boolean(Token token) : ValueItem(token), IStringItem;
}


[DebuggerDisplay("AST item: id")]
class IDItem(Token token, IEvalItemList? args = null, bool isNegative = false) : ValueItem(token), IStringItem, INumberItem {
  string Value { get; } = token.Value;

  public override string EvalStr(Current current) {
    string value = current.GetConst(Value) ?? Value;

    if (args is not null) {
      value = current.CallFunc(Value, args) ?? Value;
    }

    return isNegative ? "-" + value : value;
  }
}


[DebuggerDisplay("AST")]
class AST(IEvalItemList items) : IEvalItem {
  public string? EvalStr(Current current) {
    Eval(current);
    return null;
  }

  public virtual void Eval(Current current) {
    foreach (var child in items) {
      child.EvalStr(current);
    }
  }

  public string ToInspectStr() {
    return string.Join(' ', items);
  }
}


[DebuggerDisplay("AST item: action")]
abstract class ActionASTItem() : ASTItem() {
  public override string? EvalStr(Current current) {
    Eval(current);
    return null;
  }
}


[DebuggerDisplay("AST item: value")]
abstract class ValueASTItem() : ASTItem() {
  public override void Eval(Current current) {
    EvalStr(current);
  }
}


[DebuggerDisplay("AST item")]
abstract class ASTItem() : IEvalItem {
  public abstract void Eval(Current current);
  public abstract string? EvalStr(Current current);
  //public abstract string ToInspectStr();
}


interface INumberItem : IStringEvalItem;
interface IStringItem : IStringEvalItem;

interface IEvalItem<T> {
  void Eval(Current current);
  T EvalStr(Current current);
  //string ToInspectStr();
}


class Current {
  XDocument? _xml = null;
  public XDocument? Xml {
    get => _xml;
    set => _xml = value;
  }

  XElement? _element = null;
  public XElement? Element {
    get => _element ?? Xml?.Root;
    set => _element = value;
  }

  LinkedList<Scope> Scopes { get; set; } = [];
  Scope GlobalScope => Scopes.First!.Value;

  public Current() {
    Scope global = new();

    global.Funcs.Add("get", GetAttribute);
    Scopes.AddLast(global);
  }

  public string? GetConst(string name) {
    foreach (var scope in Scopes.Reverse()) {
      if (scope.Vars.TryGetValue(name, out string? value)) {
        return value;
      }
    }

    return null;
  }
  public void SetConst(string name, string value) {
    Scopes.Last?.Value.Vars.Add(name, value);
    Scopes.Last?.Value.Funcs.Add(name, args => {
      string copy = value;

      for (int i = 0; i < args.Count(); ++i) {
        copy = copy.Replace($"[{i + 1}]", args.ElementAt(i).EvalStr(this));
      }

      return copy;
    });
  }

  public string? CallFunc(string name, IEvalItemList? args = null) {
    foreach (var scope in Scopes.Reverse()) {
      if (scope.Funcs.TryGetValue(name, out var value)) {
        return value(args ?? []);
      }
    }
    return null;
  }

  public void SetFunc(string name, CustomFunc func) {
    Scopes.Last?.Value.Funcs.Add(name, func);
  }

  public void ResetXML() {
    _element = null;
    _xml = null;
  }

  public void WithXML(XDocument newDoc, IEvalItem item) {
    var prevDoc = _xml;

    _xml = newDoc;
    Scoped(() => item.EvalStr(this));
    _xml = prevDoc;
  }

  public void WithElement(XElement newElement, IEvalItem item) {
    var prevElement = Element;

    Element = newElement;
    Scoped(() => item.EvalStr(this));
    Element = prevElement;
  }

  public void WithScopes(LinkedList<Scope> scopes, IEvalItem item) {
    WithScopes(scopes, () => item.EvalStr(this));
  }

  public void WithScopes(LinkedList<Scope> scopes, Action action) {
    var prevScopes = Scopes;

    Scopes = scopes;
    action();
    Scopes = prevScopes;
  }

  public void Scoped(Action action) {
    Scopes.AddLast(new Scope());
    action();
    Scopes.RemoveLast();
  }

  public void Isolated(Action action) {
    LinkedList<Scope> scopes = [];

    scopes.AddFirst(GlobalScope);
    WithScopes(scopes, () => Scoped(action));
  }

  string? GetAttribute(IEvalItemList args) {
    string? selector;
    bool useRoot;
    object? value;

    if (!args.Any()) {
      return null;
    }

    selector = args.ElementAt(0).EvalStr(this);
    if (selector is null) {
      return null;
    }

    useRoot = selector.StartsWith('~');
    selector = $"string({selector.Replace("~", "")})";

    value = useRoot
      ? Xml?.XPathEvaluate(selector)
      : Element?.XPathEvaluate(selector);

    return value?.ToString();
  }

  public delegate string? CustomFunc(IEvalItemList args);

  public struct Scope() {
    public Dictionary<string, string> Vars = [];
    public Dictionary<string, CustomFunc> Funcs = [];
  }
}
