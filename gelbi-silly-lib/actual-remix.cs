using Menu.Remix.MixedUI;
using UnityEngine;

namespace gelbi_silly_lib.RemixUtils;

public static class Extensions
{
  /// <summary>
  /// Sets value of configurable and updates assigned UIconfig element
  /// </summary>
  /// <param name="onValueChange">Optional delegate, that will be invoked for given element</param>
  public static void ForceValue(this Configurable<bool> self, bool value, OnValueChangeHandler onValueChange = null)
  {
    self.Value = value;
    UIconfig element = self.BoundUIconfig;
    string oldValue = element.value;
    element.ForceValue(value ? "true" : "false");
    element.Change();
    onValueChange?.Invoke(element, element.value, oldValue);
  }

  /// <summary>
  /// Sets value of configurable and updates assigned UIconfig element
  /// </summary>
  /// <param name="onValueChange">Optional delegate, that will be invoked for given element</param>
  public static void ForceValue<T>(this Configurable<T> self, T value, OnValueChangeHandler onValueChange = null)
  {
    self.Value = value;
    UIconfig element = self.BoundUIconfig;
    string oldValue = element.value;
    element.ForceValue(value.ToString());
    element.Change();
    onValueChange?.Invoke(element, element.value, oldValue);
  }

  /// <summary>
  /// Sets text and edge color of OpTextBox to specified one
  /// </summary>
  public static void SetOutlineColor(this OpTextBox self, Color color)
  {
    self.colorEdge = color;
    self.colorText = color;
  }
}