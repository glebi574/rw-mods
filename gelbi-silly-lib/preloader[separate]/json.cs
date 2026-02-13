using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using static gelbi_silly_lib.LogWrapper;

namespace gelbi_silly_lib.Json;

/// <summary>
/// Fast json parser, mainly for use at preloader stage
/// </summary>
public class JsonParser
{
  /// <summary>
  /// Array, representing special character for each allowed 2nd character of \* sequence
  /// </summary>
  public static char[] specialChars = new char[127];
  /// <summary>
  /// Array, defining whether character signifies end of number
  /// </summary>
  public static bool[] valueChars = new bool[127];
  /// <summary>
  /// Array, defining whether character is ignored while iterating structure
  /// </summary>
  public static bool[] ignoredChars = new bool[127];

  /// <summary>
  /// Current or previous character
  /// </summary>
  public char c;
  public int length = 0;
  /// <summary>
  /// Index of start of parsed thing or index of character right after it
  /// </summary>
  public int index = 0;
  public string data;

  static JsonParser()
  {
    specialChars['n'] = '\n';
    specialChars['t'] = '\t';
    specialChars['r'] = '\r';
    specialChars['b'] = '\b';
    specialChars['f'] = '\f';
    specialChars['/'] = '/';
    specialChars['\"'] = '\"';
    specialChars['\\'] = '\\';

    valueChars['\t'] = true;
    valueChars['\n'] = true;
    valueChars['\r'] = true;
    valueChars[' '] = true;
    valueChars[','] = true;
    valueChars[']'] = true;
    valueChars['}'] = true;

    valueChars['t'] = true;
    valueChars['f'] = true;
    valueChars['n'] = true;

    ignoredChars['\t'] = true;
    ignoredChars['\n'] = true;
    ignoredChars['\r'] = true;
    ignoredChars[' '] = true;
    ignoredChars[','] = true;
    ignoredChars[':'] = true;
  }

  // index/c specify current char and may be start of thing, that's being parsed
  // value parser only retrieves char, limited by string length
  // structure parsers point to char after their end, but don't assign it

  public JsonParser(string data)
  {
    Initialize(data);
  }
  
  /// <summary>
  /// Initializes parser for given json string
  /// </summary>
  public void Initialize(string data)
  {
    this.data = data;
    length = data.Length;
    c = data[0];
  }

  /// <summary>
  /// Parses thing, based on first char
  /// </summary>
  internal object ParseCurrent()
  {
    return c switch
    {
      '\"' => ParseString(),
      '[' => ParseList(),
      '{' => ParseObject(),
      _ => ParseValue(),
    };
  }

  /// <summary>
  /// Parses object into dictionary
  /// </summary>
  internal Dictionary<string, object> ParseObject()
  {
    Dictionary<string, object> dictionary = [];
    bool isKey = true;
    string key = "";
    while (true)
    {
      c = data[++index];
      if (ignoredChars[c])
        continue;
      if (c == '}')
      {
        ++index;
        return dictionary;
      }
      if (isKey)
      {
        key = ParseString();
        isKey = false;
      }
      else
      {
        dictionary[key] = ParseCurrent();
        isKey = true;
      }
      if (data[index] == '}')
      {
        ++index;
        return dictionary;
      }
    }
  }

  /// <summary>
  /// Parses array into list
  /// </summary>
  internal List<object> ParseList()
  {
    List<object> list = [];
    while (true)
    {
      c = data[++index];
      if (ignoredChars[c])
        continue;
      if (c == ']')
      {
        ++index;
        return list;
      }
      list.Add(ParseCurrent());
      if (data[index] == ']')
      {
        ++index;
        return list;
      }
    }
  }

  /// <summary>
  /// Parses string
  /// </summary>
  internal string ParseString()
  {
    StringBuilder sb = new();
    ++index;
    while (true)
    {
      c = data[index];
      switch (c)
      {
        case '\\':
          c = data[index + 1];
          if (c == 'u')
          {
            sb.Append((char)Convert.ToInt32(data.Substring(index + 2, 4), 16));
            index += 6;
          }
          else
          {
            sb.Append(specialChars[c]);
            index += 2;
          }
          break;
        case '\"':
          ++index;
          return sb.ToString();
        default:
          sb.Append(c);
          ++index;
          break;
      }
    }
  }

  /// <summary>
  /// Parses bool, null and number values
  /// </summary>
  internal object ParseValue()
  {
    if (valueChars[c])
      switch (c)
      {
        case 't':
          index += 4;
          return true;
        case 'f':
          index += 5;
          return false;
        case 'n':
          index += 4;
          return null;
      }
    bool hasDot = false;
    int indexStart = index;
    while (++index < length)
    {
      c = data[index];
      if (c == '.')
        hasDot = true;
      if (valueChars[c])
        break;
    }
    string number = data.Substring(indexStart, index - indexStart);
    if (hasDot)
      return double.Parse(number, NumberStyles.Any, CultureInfo.InvariantCulture);
    return long.Parse(number, NumberStyles.Any, CultureInfo.InvariantCulture);
  }

  /// <summary>
  /// Parses json string into value, string, list or dictionary
  /// </summary>
  public object Parse()
  {
    try
    {
      while (true)
      {
        if (!ignoredChars[c])
          break;
        c = data[++index];
      }
      return ParseCurrent();
    }
    catch (Exception e)
    {
      LogError(e);
    }
    return null;
  }

  /// <summary>
  /// Parses json string into value, string, list or dictionary
  /// </summary>
  public static object Parse(string data)
  {
    return new JsonParser(data).Parse();
  }
}

/// <summary>
/// json serializer, mainly for use at preloader stage
/// </summary>
public class JsonSerializer
{
  /// <summary>
  /// json variants for special chars
  /// </summary>
  public static string[] parsedChars = new string[127];

  public StringBuilder builder = new();
  public bool inlineDictionaries = false;
  public int tabSize = 2, currentTabSize = 0, maxItemsToInline = 6;

  static JsonSerializer()
  {
    for (char c = ' '; c < 127; ++c)
      parsedChars[c] = c.ToString();

    parsedChars['\b'] = "\\b";
    parsedChars['\t'] = "\\t";
    parsedChars['\n'] = "\\n";
    parsedChars['\f'] = "\\f";
    parsedChars['\r'] = "\\r";
    parsedChars['\\'] = "\\\\";
    parsedChars['\"'] = "\\\"";
  }

  /// <summary>
  /// 
  /// </summary>
  /// <param name="tabSize">Amount of spaces for each new nested thing</param>
  /// <param name="maxItemsToInline">Maximum amount of items, for which list or dictionary will be inlined. Lists or dictionaries are never inlined inside each other</param>
  /// <param name="inlineDictionaries">Whether to inline dictionaries. Depends on <paramref name="maxItemsToInline"/></param>
  public JsonSerializer(int tabSize = 2, int maxItemsToInline = 6, bool inlineDictionaries = false)
  {
    this.tabSize = tabSize;
    this.maxItemsToInline = maxItemsToInline;
    this.inlineDictionaries = inlineDictionaries;
  }

  /// <summary>
  /// Resets state
  /// </summary>
  public void Reset()
  {
    builder.Clear();
  }

  /// <summary>
  /// Serializes any object
  /// </summary>
  public void SerializeAny(object obj)
  {
    if (obj == null)
    {
      builder.Append("null");
      return;
    }
    switch (obj)
    {
      case bool b:
        builder.Append(b ? "true" : "false");
        return;
      case string s:
        SerializeString(s);
        return;
      case List<object> list:
        SerializeList(list);
        return;
      case Dictionary<string, object> dictionary:
        SerializeDictionary(dictionary);
        return;
      default:
        if (obj is int or uint or long or ulong or byte or sbyte or short or ushort)
          builder.Append(obj.ToString());
        else if (obj is float or double or decimal)
          builder.Append(((IFormattable)obj).ToString("R", CultureInfo.InvariantCulture));
        else
          SerializeString(obj.ToString());
          return;
    }
  }

  /// <summary>
  /// Serializes dictionary
  /// </summary>
  public void SerializeDictionary(Dictionary<string, object> dictionary)
  {
    if (dictionary.Count == 0)
    {
      builder.Append("{}");
      return;
    }
    builder.Append('{');
    foreach (KeyValuePair<string, object> pair in dictionary)
    {
      SerializeString(pair.Key);
      builder.Append(':');
      SerializeAny(pair.Value);
      builder.Append(',');
    }
    builder.Remove(builder.Length - 1, 1);
    builder.Append('}');
  }

  /// <summary>
  /// Serializes list
  /// </summary>
  public void SerializeList(List<object> list)
  {
    if (list.Count == 0)
    {
      builder.Append("[]");
      return;
    }
    builder.Append('[');
    foreach (object value in list)
    {
      SerializeAny(value);
      builder.Append(',');
    }
    builder.Remove(builder.Length - 1, 1);
    builder.Append(']');
  }

  /// <summary>
  /// Serializes string
  /// </summary>
  public void SerializeString(string str)
  {
    builder.Append('\"');
    foreach (char c in str)
    {
      if (c < 127)
        builder.Append(parsedChars[c]);
      else
        builder.Append(c);
      // builder.Append($"\\u{(int)c:X4}");
    }
    builder.Append('\"');
  }

  internal void AppendNewLine()
  {
    builder.Append('\n');
    builder.Append(' ', currentTabSize);
  }

  /// <summary>
  /// Serializes any object in a readable way
  /// </summary>
  public void SerializeAnyE(object obj)
  {
    if (obj == null)
    {
      builder.Append("null");
      return;
    }
    switch (obj)
    {
      case bool b:
        builder.Append(b ? "true" : "false");
        return;
      case string s:
        SerializeString(s);
        return;
      case List<object> list:
        SerializeListE(list);
        return;
      case Dictionary<string, object> dictionary:
        SerializeDictionaryE(dictionary);
        return;
      default:
        if (obj is int or uint or long or ulong or byte or sbyte or short or ushort)
          builder.Append(obj.ToString());
        else if (obj is float or double or decimal)
          builder.Append(((IFormattable)obj).ToString("R", CultureInfo.InvariantCulture));
        else
          SerializeString(obj.ToString());
        return;
    }
  }

  /// <summary>
  /// Serializes dictionary in a readable way
  /// </summary>
  public void SerializeDictionaryE(Dictionary<string, object> dictionary)
  {
    if (dictionary.Count == 0)
    {
      builder.Append("{}");
      return;
    }
    bool inline = (dictionary.Count <= maxItemsToInline) && inlineDictionaries;
    builder.Append('{');
    currentTabSize += tabSize;
    if (!inline || dictionary.First().Value is ICollection)
      AppendNewLine();
    foreach (KeyValuePair<string, object> pair in dictionary)
    {
      SerializeString(pair.Key);
      builder.Append(": ");
      SerializeAnyE(pair.Value);
      builder.Append(',');
      if (inline && pair.Value is not ICollection)
        builder.Append(' ');
      else
        AppendNewLine();
    }
    builder.Remove(builder.Length - tabSize, tabSize);
    currentTabSize -= tabSize;
    builder.Append('}');
  }

  /// <summary>
  /// Serializes list in a readable way
  /// </summary>
  public void SerializeListE(List<object> list)
  {
    if (list.Count == 0)
    {
      builder.Append("[]");
      return;
    }
    bool inline = list.Count <= maxItemsToInline;
    builder.Append('[');
    currentTabSize += tabSize;
    if (!inline || list[0] is ICollection)
      AppendNewLine();
    foreach (object value in list)
    {
      SerializeAnyE(value);
      builder.Append(',');
      if (inline && value is not ICollection)
        builder.Append(' ');
      else
        AppendNewLine();
    }
    builder.Remove(builder.Length - 2, 2);
    currentTabSize -= tabSize;
    builder.Append(']');
  }

  /// <summary>
  /// Serializes object into json string
  /// </summary>
  /// <param name="obj">Object to serialize</param>
  /// <param name="makeReadable">Whether to add tabs, to make output readable. All following options don't matter if set to <c>false</c></param>
  /// <param name="tabSize">Amount of spaces for each new nested thing</param>
  /// <param name="maxItemsToInline">Maximum amount of items, for which list or dictionary will be inlined. Lists or dictionaries are never inlined inside each other</param>
  /// <param name="inlineDictionaries">Whether to inline dictionaries. Depends on <paramref name="maxItemsToInline"/></param>
  public static string Serialize(object obj, bool makeReadable = false, int tabSize = 2, int maxItemsToInline = 6, bool inlineDictionaries = false)
  {
    JsonSerializer serializer = new(tabSize, maxItemsToInline, inlineDictionaries);
    if (makeReadable)
      serializer.SerializeAnyE(obj);
    else
      serializer.SerializeAny(obj);
    return serializer.builder.ToString();
  }
}