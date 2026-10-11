using System;
using System.Collections.Generic;

namespace gelbi_silly_lib.Converter;

/// <summary>
/// Converter extensions mainly for JSON reading
/// </summary>
public static class Extensions
{
  /// <summary>
  /// Attempts to retrieve value with specified type. Use more specific overrides when parsing JSON data or ensure your data is of one of the possible types, as otherwise this method will silently fail.
  /// </summary>
  public static bool TryGetValueWithType<T, TKey, TValue>(this Dictionary<TKey, TValue> self, TKey name, out T result)
  {
    if (self.TryGetValue(name, out TValue value) && value is T converted)
    {
      result = converted;
      return true;
    }
    result = default;
    return false;
  }

  public static void TryUpdateValueWithType<T, TKey, TValue>(this Dictionary<TKey, TValue> self, TKey name, ref T result)
  {
    if (self.TryGetValueWithType(name, out T value))
      result = value;
  }

  extension<TKey>(Dictionary<TKey, object> self)
  {
    public bool TryGetNumber(TKey name, out byte result)
    {
      if (!self.TryGetValue(name, out object value))
      {
        result = default;
        return false;
      }
      result = Convert.ToByte(value);
      return true;
    }
    public bool TryGetNumber(TKey name, out sbyte result)
    {
      if (!self.TryGetValue(name, out object value))
      {
        result = default;
        return false;
      }
      result = Convert.ToSByte(value);
      return true;
    }
    public bool TryGetNumber(TKey name, out short result)
    {
      if (!self.TryGetValue(name, out object value))
      {
        result = default;
        return false;
      }
      result = Convert.ToInt16(value);
      return true;
    }
    public bool TryGetNumber(TKey name, out ushort result)
    {
      if (!self.TryGetValue(name, out object value))
      {
        result = default;
        return false;
      }
      result = Convert.ToUInt16(value);
      return true;
    }
    public bool TryGetNumber(TKey name, out int result)
    {
      if (!self.TryGetValue(name, out object value))
      {
        result = default;
        return false;
      }
      result = Convert.ToInt32(value);
      return true;
    }
    public bool TryGetNumber(TKey name, out uint result)
    {
      if (!self.TryGetValue(name, out object value))
      {
        result = default;
        return false;
      }
      result = Convert.ToUInt32(value);
      return true;
    }
    public bool TryGetNumber(TKey name, out long result)
    {
      if (self.TryGetValue(name, out object value))
      {
        result = Convert.ToInt64(value);
        return true;
      }
      result = default;
      return false;
    }
    public bool TryGetNumber(TKey name, out ulong result)
    {
      if (!self.TryGetValue(name, out object value))
      {
        result = default;
        return false;
      }
      result = Convert.ToUInt64(value);
      return true;
    }
    public bool TryGetNumber(TKey name, out float result)
    {
      if (self.TryGetValue(name, out object value))
      {
        result = Convert.ToSingle(value);
        return true;
      }
      result = default;
      return false;
    }
    public bool TryGetNumber(TKey name, out double result)
    {
      if (self.TryGetValue(name, out object value))
      {
        result = Convert.ToDouble(value);
        return true;
      }
      result = default;
      return false;
    }

    public void TryUpdateNumber(TKey name, ref byte result)
    {
      if (self.TryGetNumber(name, out byte value))
        result = value;
    }
    public void TryUpdateNumber(TKey name, ref sbyte result)
    {
      if (self.TryGetNumber(name, out sbyte value))
        result = value;
    }
    public void TryUpdateNumber(TKey name, ref short result)
    {
      if (self.TryGetNumber(name, out short value))
        result = value;
    }
    public void TryUpdateNumber(TKey name, ref ushort result)
    {
      if (self.TryGetNumber(name, out ushort value))
        result = value;
    }
    public void TryUpdateNumber(TKey name, ref uint result)
    {
      if (self.TryGetNumber(name, out uint value))
        result = value;
    }
    public void TryUpdateNumber(TKey name, ref int result)
    {
      if (self.TryGetNumber(name, out int value))
        result = value;
    }
    public void TryUpdateNumber(TKey name, ref long result)
    {
      if (self.TryGetNumber(name, out long value))
        result = value;
    }
    public void TryUpdateNumber(TKey name, ref ulong result)
    {
      if (self.TryGetNumber(name, out ulong value))
        result = value;
    }
    public void TryUpdateNumber(TKey name, ref float result)
    {
      if (self.TryGetNumber(name, out float value))
        result = value;
    }
    public void TryUpdateNumber(TKey name, ref double result)
    {
      if (self.TryGetNumber(name, out double value))
        result = value;
    }

    public bool TryGetEnumFromNumber<T>(TKey name, out T result) where T : Enum
    {
      if (self.TryGetNumber(name, out long value))
      {
        result = (T)Enum.ToObject(typeof(T), value);
        return true;
      }
      result = default;
      return false;
    }

    public void TryUpdateEnumFromNumber<T>(TKey name, ref T result) where T : Enum
    {
      if (self.TryGetEnumFromNumber(name, out T value))
        result = value;
    }
  }

  extension(List<object> self)
  {
    public void UpdateNumber(int index, ref int result) => result = Convert.ToInt32(self[index]);

    public void UpdateNumber(int index, ref long result) => result = Convert.ToInt64(self[index]);

    public void UpdateNumber(int index, ref float result) => result = Convert.ToSingle(self[index]);

    public void UpdateNumber(int index, ref double result) => result = Convert.ToDouble(self[index]);
  }

  extension(string[] self)
  {
    public void UpdateValue(int index, ref bool result) => result = Convert.ToBoolean(self[index]);

    public void UpdateNumber(int index, ref int result) => result = Convert.ToInt32(self[index]);

    public void UpdateNumber(int index, ref long result) => result = Convert.ToInt64(self[index]);

    public void UpdateNumber(int index, ref float result) => result = Convert.ToSingle(self[index]);

    public void UpdateNumber(int index, ref double result) => result = Convert.ToDouble(self[index]);
  }
}