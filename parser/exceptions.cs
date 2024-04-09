using sr_mml_lang.lexer;

namespace sr_mml_lang.parser;

class ParseException(string message) : Exception(message);

class UnexpectedTokenException : ParseException {
  public UnexpectedTokenException(in Token? token)
    : base($"Unexpected token \"{token?.Value}\"") { }

  public UnexpectedTokenException(in Token? found, string expected)
    : base($"Unexpected token \"{found?.Value}\". Expected {expected}. Found {found?.Type.ToString() ?? "None"}") { }
}

class NoXMLDocException() : ParseException("XML document is missed on evaluate");
