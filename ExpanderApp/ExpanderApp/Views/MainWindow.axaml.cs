using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace ExpanderApp.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        Console.WriteLine();
        Console.WriteLine("MainWindow:");
        if (Content is StyledElement content)
        {
            foreach(var child in content.GetAllChildren(nameof(Content), true))
                Console.WriteLine($"{child.PropertyPath} => {child.Element.GetElementPath()}");

            foreach (var style in content.Styles)
            {
                Console.WriteLine($"Style = {style}");
            }
        }

        if (Content is Expander expander)
        {
            Console.WriteLine();
            StyleDiagnostics.PrintStyles(expander);
            Console.WriteLine();
            TemplateStyleDiagnostics.PrintTemplateStyles(expander);
        }

    }
}

public static class StyleDiagnostics
{
    public static void PrintStyles(StyledElement element)
    {
        Console.WriteLine($"\n=== Styles for {element.GetType().Name} ===");

        // Get styles from the element itself
        Console.WriteLine("\nDirect Styles:");
        foreach (var style in element.Styles)
        {
            Console.WriteLine($"  {style}");
        }

        // Walk up the tree to get inherited styles
        Console.WriteLine("\nInherited Styles:");
        var parent = element.Parent as StyledElement;
        while (parent != null)
        {
            foreach (var style in parent.Styles)
            {
                Console.WriteLine($"  From {parent.GetType().Name}: {style}");
            }
            parent = parent.Parent as StyledElement;
        }

        // Get application-level styles
        if (Application.Current != null)
        {
            Console.WriteLine("\nApplication Styles:");
            foreach (var style in Application.Current.Styles)
            {
                Console.WriteLine($"  {style}");
            }
        }
    }
}

public static class TemplateStyleDiagnostics
{
    public static void PrintTemplateStyles(TemplatedControl control)
    {
        Console.WriteLine($"\n=== Template Styles for {control.GetType().Name} ===");

        // Apply template if not already applied
        control.ApplyTemplate();

        // Get visual children (template parts)
        Console.WriteLine("\nTemplate Parts:");
        EnumerateVisualChildren(control, 0);
    }

    private static void EnumerateVisualChildren(Visual visual, int depth)
    {
        var indent = new string(' ', depth * 2);

        foreach (var child in visual.GetVisualChildren())
        {
            Console.WriteLine($"{indent}{child.GetType().Name}");

            if (child is StyledElement styledChild)
            {
                // Print classes applied to this element
                if (styledChild.Classes.Count > 0)
                {
                    Console.WriteLine($"{indent}  Classes: {string.Join(", ", styledChild.Classes)}");
                }

                // Print direct styles
                if (styledChild.Styles.Count > 0)
                {
                    Console.WriteLine($"{indent}  Styles: {styledChild.Styles.Count}");
                }
            }

            EnumerateVisualChildren(child, depth + 1);
        }
    }
}

public static class StyledElementExtensions
{
    public static string GetElementPath(this StyledElement? element)
    {
        if (element == null)
            return string.Empty;

        var type = element.GetType().Name;
        var name = string.IsNullOrWhiteSpace(element.Name)
            ? string.Empty
            : $"#{element.Name}";
        var classes = element.Classes.Count == 0
            ? string.Empty
            : string.Join("", element.Classes.Select(c => $".{c}"));

        return $"{GetElementPath(element.Parent)}/{type}{name}{classes}";
    }

    public static IEnumerable<(string PropertyPath, StyledElement Element)> GetAllChildren(this StyledElement element, string nameOfProperty, bool includeSelf = false, HashSet<StyledElement>? visited = null)
    {
        if (visited == null)
            visited = [];

        if (includeSelf && !visited.Contains(element))
            yield return (nameOfProperty, element);
        visited.Add(element);

        foreach (var property in element.GetType().GetProperties())
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0 || property.Name == "Parent")
                continue;

            object? value = TryGetValue(element, property);

            if (value is StyledElement onlyChild)
            {
                if (!visited.Add(onlyChild))
                    continue;

                var name = $"{nameOfProperty}.{property.Name}";
                yield return (name, onlyChild);
                foreach (var grandChild in GetAllChildren(onlyChild, name, false, visited))
                    yield return grandChild;
            }
            else if (value is IEnumerable<StyledElement> children)
            {
                int index = -1;
                foreach (var child in children)
                {
                    index++;
                    if (!visited.Add(child))
                        continue;

                    var  name = $"{nameOfProperty}.{property.Name}[{index}]";
                    yield return (name, child);
                    foreach (var grandChild in GetAllChildren(child, nameOfProperty, false, visited))
                        yield return grandChild;
                }
            }
        }
    }

    private static object? TryGetValue(StyledElement element, PropertyInfo property)
    {
        try
        {
            return property.GetValue(element);
        }
        catch
        {
            return null;
        }
    }
}
