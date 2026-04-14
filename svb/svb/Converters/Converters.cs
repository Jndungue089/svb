using System.Globalization;

namespace BeneditaUI.Converters;

// ── InverseBool ───────────────────────────────────────────────
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) =>
        v is bool b && !b;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) =>
        v is bool b && !b;
}

// ── bool → Color (true=Success green, false=Danger red) ──────
public class BoolToColorConverter : IValueConverter
{
    public Color TrueColor  { get; set; } = Color.FromArgb("#1E90FF");
    public Color FalseColor { get; set; } = Color.FromArgb("#1D4ED8");

    public object Convert(object? v, Type t, object? p, CultureInfo c) =>
        v is bool b && b ? TrueColor : FalseColor;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) =>
        throw new NotImplementedException();
}

// ── bool → API status text ────────────────────────────────────
public class BoolToStatusConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) =>
        v is true ? "API Online" : "API Offline";
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) =>
        throw new NotImplementedException();
}

// ── bool → Connect/Disconnect label ──────────────────────────
public class BoolToConnectLabelConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) =>
        v is true ? "Desligar" : "Ligar";
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) =>
        throw new NotImplementedException();
}

// ── null/empty → bool ─────────────────────────────────────────
public class NullToBoolConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) =>
        v is not null && (v is not string s || !string.IsNullOrWhiteSpace(s));
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) =>
        throw new NotImplementedException();
}

// ── double (0–100) → double (0.0–1.0) ────────────────────────
public class PercentToProgressConverter : IValueConverter
{
    public object Convert(object? v, Type t, object? p, CultureInfo c) =>
        v is double d ? d / 100.0 : 0.0;
    public object ConvertBack(object? v, Type t, object? p, CultureInfo c) =>
        throw new NotImplementedException();
}

