using sr_mml_lang.lexer;
using sr_mml_lang.lexer.keywords;
using sr_mml_lang.lexer.operators;

namespace sr_mml_lang.parser;

using ItemList = List<ASTItem>;

class Parser() {
  TokenList tokens = [];

  int currentIndex = 0;

  Token? CurrentToken => tokens.GetAt(currentIndex);
  Token? NextToken => tokens.GetAt(currentIndex + 1);
  bool HasNext => NextToken is not null;

  public delegate string? FileReader(string name);
  FileReader ReadFile { get; set; } = _ => null;

  public void Parse(string str, FileReader readFile) {
    Parse(new Lexer().Lex(str), readFile);
  }
  public void Parse(TokenList tokens, FileReader readFile) {
    this.tokens = tokens;
    ReadFile = readFile;
    currentIndex = 0;

    if (!tokens.HasItems) {
      return;
    }

    new AST(ParseTokens(ParseItems, _ => HasNext)).Eval(new());
  }

  bool ParseFile(ItemList items) {
    if (!CheckKeyword(Keyword.File)) {
      return false;
    }

    items.Add(new FileItem(
      ParseString(),
      ParseItemsInBlock()
    ));

    return true;
  }

  bool ParseInclude(ItemList items) {
    if (!CheckKeyword(Keyword.Include)) {
      return false;
    }

    items.Add(new IncludeItem(ParseID(), ReadFile));
    return true;
  }

  bool ParseConst(ItemList items) {
    IDItem id;
    ValueItem value;

    if (!CheckKeyword(Keyword.Const)) {
      return false;
    }

    id = ParseID();
    ParseOperator(Operator.Equals);
    value = ParseValue();
    items.Add(new ConstItem(id, value));

    return true;
  }

  bool ParseRemove(ItemList items) {
    return CheckKeyword(Keyword.Remove) && (
      ParseRemoveAllElements(items) ||
      ParseRemoveAttribute(items) ||
      ParseRemoveElement(items)
    );

    bool ParseRemoveElement(ItemList items) {
      items.Add(new RemoveItem.Element(ParseString()));
      return true;
    }

    bool ParseRemoveAllElements(ItemList items) {
      if (!CheckKeyword(Keyword.All)) {
        return false;
      }

      items.Add(new RemoveItem.Element.All(ParseString()));
      return true;
    }

    bool ParseRemoveAttribute(ItemList items) {
      items.Add(new RemoveItem.Attribute(ParseString()));
      return true;
    }
  }

  bool ParseChangeElement(ItemList items) {
    return
      ParseChangeOrAddElement(items) ||
      ParseChangeAllElements(items) ||
      ParseChangeSingleElement(items);


    bool ParseChangeSingleElement(ItemList items) {
      if (!CheckKeyword(Keyword.Change)) {
        return false;
      }

      items.Add(new ChangeElementItem(
        ParseString(),
        ParseItemsInBlock()
      ));

      return true;
    }

    bool ParseChangeOrAddElement(ItemList items) {
      IStringItem tag;
      INumberItem? position;
      AST ast;

      if (!CheckKeyword(Keyword.Change_Add)) {
        return false;
      }

      tag = ParseString();
      position = TryParsePosition();
      ast = ParseItemsInBlock();
      items.Add(new ChangeElementItem.OrAdd(tag, ast, position));

      return true;
    }

    bool ParseChangeAllElements(ItemList items) {
      if (!CheckKeyword(Keyword.All)) {
        return false;
      }

      items.Add(new ChangeElementItem.All(
        ParseString(),
        ParseItemsInBlock()
      ));

      return true;
    }
  }

  bool ParseAddElement(ItemList items) {
    IStringItem tag;
    INumberItem? position;
    AST ast;

    if (!CheckKeyword(Keyword.Add)) {
      return false;
    }

    tag = ParseString();
    position = TryParsePosition();
    ast = ParseItemsInBlock();
    items.Add(new AddElementItem(tag, ast, position));

    return true;
  }

  bool ParseSetAttr(ItemList items) {
    IStringItem tag;
    ValueItem value;

    if (!CheckKeyword(Keyword.Set)) {
      return false;
    }

    tag = ParseString();
    ParseOperator(Operator.Equals);
    value = ParseValue();
    items.Add(new SetAttrItem(tag, value));

    return true;
  }

  bool ParsePreset(ItemList items) {
    IDItem id;
    ItemList args;
    AST ast;

    if (!CheckKeyword(Keyword.Preset)) {
      return false;
    }

    id = ParseID();
    args = ParseArgs(true);
    ast = ParseItemsInBlock();
    items.Add(new PresetItem(id, ast, args));

    return true;
  }

  bool ParseUse(ItemList items) {
    if (!CheckKeyword(Keyword.Use)) {
      return false;
    }

    items.Add(new UseItem(
      ParseID(),
      ParseArgs()
    ));

    return true;
  }

  AST ParseItemsInBlock() {
    return new(ParseBlock(ParseItems));
  }

  delegate bool ItemsParser(ItemList items);
  bool ParseItems(ItemList items) {
    ItemsParser[] parsers = [
      ParseInclude,
      ParseFile,
      ParseUse,
      ParsePreset,
      ParseSetAttr,
      ParseAddElement,
      ParseChangeElement,
      ParseRemove,
      ParseConst
    ];

    foreach (var parser in parsers) {
      if (parser(items)) {
        return true;
      }
    }

    return false;
  }

  ItemList ParseBlock(ASTAction action) {
    ItemList items;

    ParseOperator(Operator.OpenBrace);
    items = ParseTokens(action, token => !token.IsOperator(Operator.CloseBrace));
    ParseOperator(Operator.CloseBrace);

    return items;
  }

  ItemList ParseArgs(bool isDefine = false) {
    return TryParseArgs(isDefine)
      ?? throw new ParseException("Missing arguments on calling function");
  }

  delegate bool ASTAction(ItemList items);
  ItemList ParseTokens(ASTAction action, Func<Token, bool>? checkToken = null) {
    ItemList items = [];

    while (CurrentToken is not null && (checkToken is null || checkToken(CurrentToken))) {
      if (!action(items)) {
        throw new UnexpectedTokenException(CurrentToken);
      }
    }

    return items;
  }

  IDItem ParseID() {
    return new(ParseToken(token => token.IsIdentifier(), "Identifier"));
  }

  INumberItem ParseInt() {
    bool isNegative = CheckOperator(Operator.Minus);

    var token = ParseToken(token =>
      token.TypeIs([TokenType.Number, TokenType.Identifier]),
      "Integer or Variable"
    );

    return token.Type switch {
      TokenType.Identifier => new IDItem(token, TryParseArgs(), isNegative),
      _ => new ValueItem.Number.Int(token, isNegative)
    };
  }

  IStringItem ParseString() {
    var token = ParseToken(token =>
      token.TypeIs([TokenType.String, TokenType.Identifier]),
      "String or Variable"
    );

    return token.Type switch {
      TokenType.Identifier => new IDItem(token, TryParseArgs()),
      _ => new ValueItem.String(token)
    };
  }

  ValueItem ParseValue() {
    bool isNegative = CheckOperator(Operator.Minus);

    var token = ParseToken(token =>
      token.TypeIs([
        TokenType.Number,
        TokenType.String,
        TokenType.Boolean,
        TokenType.Identifier
      ]),
      "Number or String or Variable"
    );

    return token.Type switch {
      TokenType.Identifier => new IDItem(token, TryParseArgs(), isNegative),
      TokenType.Boolean => new ValueItem.Boolean(token),
      TokenType.String => new ValueItem.String(token),
      TokenType.Number => new ValueItem.Number(token, isNegative),
      _ => new ValueItem(token),
    };
  }

  ItemList? TryParseArgs(bool isDefine = false) {
    ItemList items;

    if (!CheckOperator(Operator.OpenParen)) {
      return null;
    }
    if (CheckOperator(Operator.CloseParen)) {
      return [];
    }

    items = ParseTokens(items => {
      items.Add(isDefine ? ParseID() : ParseValue());
      CheckOperator(Operator.Comma);

      return true;
    }, token => !token.IsOperator(Operator.CloseParen));

    ParseOperator(Operator.CloseParen);

    return items;
  }

  INumberItem? TryParsePosition() {
    return CheckKeyword(Keyword.At) ? ParseInt() : null;
  }

  void ParseOperator(Operator @operator) {
    ParseToken(token => token.IsOperator(@operator), $"Operator '{@operator}'");
  }

  Token ParseToken(Func<Token, bool> checkToken, string expected) {
    return CheckToken(checkToken)
      ?? throw new UnexpectedTokenException(CurrentToken, expected);
  }

  bool CheckOperator(Operator @operator) {
    return CheckToken(token => token.IsOperator(@operator)) is not null;
  }

  Token? CheckToken(Func<Token, bool> checkToken) {
    Token? token = CurrentToken;

    if (token is null || !checkToken(token))
      return null;

    Next();
    return token;
  }

  Token? Next() {
    ++currentIndex;
    return CurrentToken;
  }

  bool CheckKeyword(Keyword keyword) {
    return CheckToken(token => token.IsKeyword(keyword)) is not null;
  }
}
