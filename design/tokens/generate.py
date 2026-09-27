#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Design-token generator for Реп'ях (Burdock). Single source of truth: design/tokens/tokens.json (W3C DTCG).

Usage:
  python design/tokens/generate.py --check            validate aliases, WCAG contrast, XAML hex lint, generated-file drift (CI gate)
  python design/tokens/generate.py --out xaml         MAUI Colors.xaml / Styles.xaml / WidgetThemes/*.xaml
  python design/tokens/generate.py --out android      Android values/colors_tokens.xml, values-night/colors_tokens.xml, dimens_tokens.xml
  python design/tokens/generate.py --out swift        Swift DesignTokens.swift for the WidgetKit extension
  python design/tokens/generate.py --out all          everything above
  python design/tokens/generate.py --seed             re-run the OKLCH ramp seed (_seed_ramps.py) that rewrites tokens.json

Spec: docs/specs/02-design-system.md section 3.16 (T20, ADR 0013). Generated files carry a header and must never be edited by hand.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
REPO = HERE.parent.parent
TOKENS_PATH = HERE / "tokens.json"
PRESENTATION = REPO / "src" / "mobile" / "Burdock.Presentation"
STYLES_DIR = PRESENTATION / "Resources" / "Styles"
ANDROID_RES = PRESENTATION / "Platforms" / "Android" / "Resources"
SWIFT_DIR = REPO / "src" / "mobile" / "ios-extensions" / "BurdockWidgets" / "Shared"

HEADER_NOTE = "GENERATED from design/tokens/tokens.json by design/tokens/generate.py. Do not edit by hand; change the tokens and regenerate."

sys.stdout.reconfigure(encoding="utf-8")

# ----------------------------------------------------------------------------- tokens


def load_tokens() -> dict:
    return json.loads(TOKENS_PATH.read_text(encoding="utf-8"))


class Tokens:
    def __init__(self, data: dict):
        self.data = data

    def node(self, path: str) -> dict:
        cur = self.data
        for part in path.split("."):
            if not isinstance(cur, dict) or part not in cur:
                raise KeyError(path)
            cur = cur[part]
        return cur

    def resolve(self, value, depth: int = 0):
        """Follow {alias} references until a literal value is reached."""
        if depth > 16:
            raise ValueError(f"alias loop near {value}")
        if isinstance(value, str) and value.startswith("{") and value.endswith("}"):
            target = self.node(value[1:-1])
            return self.resolve(target["$value"], depth + 1)
        return value

    def color(self, path: str) -> str:
        return normalize_hex(self.resolve(self.node(path)["$value"]))

    def semantic(self, mode: str) -> dict[str, dict[str, str]]:
        out: dict[str, dict[str, str]] = {}
        for group, items in self.data["color"]["semantic"][mode].items():
            if group.startswith("$"):
                continue
            out[group] = {}
            for name, token in items.items():
                if name.startswith("$"):
                    continue
                out[group][name] = normalize_hex(self.resolve(token["$value"]))
        return out

    def widget_themes(self) -> dict[str, dict[str, dict[str, str]]]:
        """theme -> mode -> widget key -> hex"""
        themes: dict[str, dict[str, dict[str, str]]] = {}
        for key, theme in self.data["color"]["theme"]["widget"].items():
            if key.startswith("$"):
                continue
            themes[key] = {}
            for mode in ("light", "dark"):
                widget = theme.get(mode, {}).get("widget", {})
                themes[key][mode] = {
                    name: normalize_hex(self.resolve(token["$value"]))
                    for name, token in widget.items() if not name.startswith("$")
                }
        return themes

    def primitives(self) -> dict[str, dict[str, str]]:
        return {
            fam: {step: normalize_hex(tok["$value"]) for step, tok in ramp.items() if not step.startswith("$")}
            for fam, ramp in self.data["color"]["primitive"].items() if not fam.startswith("$")
        }

    def dimension(self, path: str) -> float:
        return parse_pt(self.resolve(self.node(path)["$value"]))

    def type_roles(self) -> list[tuple[str, str, dict]]:
        roles = []
        for role, sizes in self.data["typography"]["scale"].items():
            if role.startswith("$"):
                continue
            for size, token in sizes.items():
                if size.startswith("$"):
                    continue
                spec = dict(token["$value"])
                spec["fontFamily"] = self.resolve(spec["fontFamily"])
                roles.append((role, size, spec))
        return roles


def normalize_hex(value: str) -> str:
    if not isinstance(value, str) or not re.fullmatch(r"#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?", value):
        raise ValueError(f"not a hex colour: {value!r}")
    return value.upper()


def to_argb(value: str) -> str:
    """Tokens use CSS #RRGGBBAA; MAUI and Android expect #AARRGGBB."""
    v = normalize_hex(value)
    return f"#{v[7:9]}{v[1:7]}" if len(v) == 9 else v


def parse_pt(value) -> float:
    if isinstance(value, (int, float)):
        return float(value)
    m = re.fullmatch(r"(-?[0-9.]+)(pt|px|dp|ms|em)?", str(value).strip())
    if not m:
        raise ValueError(f"cannot parse dimension {value!r}")
    return float(m.group(1))


def pascal(name: str) -> str:
    return "".join(part[:1].upper() + part[1:] for part in re.split(r"[.\-_ ]", name) if part)


def snake(name: str) -> str:
    s = re.sub(r"(?<!^)(?=[A-Z])", "_", name).lower()
    return re.sub(r"[.\-]", "_", s)


def fmt_num(x: float) -> str:
    return ("%.4f" % x).rstrip("0").rstrip(".") if x != int(x) else str(int(x))


# ----------------------------------------------------------------------------- contrast


def _lin(c: int) -> float:
    c /= 255
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def luminance(hex_value: str) -> float:
    v = normalize_hex(hex_value)
    r, g, b = int(v[1:3], 16), int(v[3:5], 16), int(v[5:7], 16)
    return 0.2126 * _lin(r) + 0.7152 * _lin(g) + 0.0722 * _lin(b)


def contrast(fg: str, bg: str) -> float:
    la, lb = luminance(fg), luminance(bg)
    if la < lb:
        la, lb = lb, la
    return (la + 0.05) / (lb + 0.05)


def contrast_pairs(sem: dict[str, dict[str, str]]) -> list[tuple[str, str, float]]:
    """(foreground path, background path, required ratio) derived from naming conventions and spec 02 rules."""
    pairs: list[tuple[str, str, float]] = []
    for group, items in sem.items():
        for name in items:
            if len(name) > 2 and name.startswith("on") and name[2].isupper():
                base = name[2].lower() + name[3:]
                if base in items:
                    pairs.append((f"{group}.{name}", f"{group}.{base}", 4.5))
    for text in ("primary", "secondary", "tertiary"):
        for surface in ("default", "elevated", "sunken"):
            pairs.append((f"text.{text}", f"surface.{surface}", 4.5))
    # text.disabled is exempt from WCAG contrast (inactive UI); streak.frozen is an icon fill, its text sits on streak.container.
    pairs += [
        ("text.link", "surface.default", 4.5),
        ("border.strong", "surface.default", 3.0),
        ("border.focus", "surface.default", 3.0),
        ("brand.accentText", "surface.default", 4.5),
        ("feedback.success", "surface.default", 4.5),
        ("feedback.error", "surface.default", 4.5),
        ("feedback.warning", "surface.default", 4.5),
        ("feedback.info", "surface.default", 4.5),
        ("reward.goldText", "reward.goldContainer", 4.5),
        ("reward.currencyText", "reward.currencyContainer", 4.5),
        ("streak.text", "streak.container", 4.5),
        ("streak.frozenText", "streak.container", 4.5),
        ("quiz.correct", "quiz.correctContainer", 4.5),
        ("quiz.incorrect", "quiz.incorrectContainer", 4.5),
        ("quiz.selected", "quiz.selectedContainer", 4.5),
        ("quiz.correctBorder", "surface.default", 3.0),
        ("quiz.incorrectBorder", "surface.default", 3.0),
        ("widget.word", "widget.background", 4.5),
        ("widget.ipa", "widget.background", 4.5),
        ("widget.translation", "widget.background", 4.5),
        ("widget.example", "widget.background", 4.5),
        ("widget.chipText", "widget.chipBackground", 4.5),
    ]
    existing = []
    for fg, bg, req in pairs:
        g1, n1 = fg.split("."); g2, n2 = bg.split(".")
        if n1 in sem.get(g1, {}) and n2 in sem.get(g2, {}):
            existing.append((fg, bg, req))
    return existing


# ----------------------------------------------------------------------------- emitters


def emit_colors_xaml(t: Tokens) -> str:
    lines = [
        '<?xml version="1.0" encoding="UTF-8" ?>',
        f"<!-- {HEADER_NOTE} -->",
        '<ResourceDictionary xmlns="http://schemas.microsoft.com/dotnet/2021/maui"',
        '                    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">',
        "",
        "    <!-- Primitive ramps (color.primitive.*). Use semantic keys in app code; primitives exist for the generator and illustrations. -->",
    ]
    for fam, ramp in t.primitives().items():
        for step, hex_value in ramp.items():
            lines.append(f'    <Color x:Key="{pascal(fam)}{step}">{to_argb(hex_value)}</Color>')
    light, dark = t.semantic("light"), t.semantic("dark")
    lines += ["", "    <!-- Semantic colours (color.semantic.light|dark.*): Color{Group}{Name}Light / Color{Group}{Name}Dark. -->"]
    for group in light:
        lines.append(f"    <!-- {group} -->")
        for name in light[group]:
            key = f"Color{pascal(group)}{pascal(name)}"
            lines.append(f'    <Color x:Key="{key}Light">{to_argb(light[group][name])}</Color>')
            lines.append(f'    <Color x:Key="{key}Dark">{to_argb(dark[group][name])}</Color>')
    lines += ["", "    <!-- Theme-aware brushes: Brush{Group}{Name} follows the system theme through AppThemeBinding. -->"]
    for group in light:
        for name in light[group]:
            key = f"{pascal(group)}{pascal(name)}"
            lines.append(
                f'    <SolidColorBrush x:Key="Brush{key}" Color="{{AppThemeBinding Light={{StaticResource Color{key}Light}}, Dark={{StaticResource Color{key}Dark}}}}" />'
            )
    lines += ["", "</ResourceDictionary>", ""]
    return "\n".join(lines)


FONT_TEXT = {400: "FixelText", 500: "FixelTextMedium", 600: "FixelTextSemiBold", 700: "FixelTextBold"}
FONT_DISPLAY = {600: "FixelDisplay", 700: "FixelDisplay", 800: "FixelDisplayExtraBold"}


def font_alias(family: list[str] | str, weight: int) -> str | None:
    fam = family[0] if isinstance(family, list) else str(family)
    if fam.startswith("Fixel Display"):
        return FONT_DISPLAY.get(int(weight), "FixelDisplay")
    if fam.startswith("Fixel Text"):
        return FONT_TEXT.get(int(weight), "FixelText")
    return None  # IPA and system roles use the platform font


def theme_binding(key: str) -> str:
    return f"{{AppThemeBinding Light={{StaticResource Color{key}Light}}, Dark={{StaticResource Color{key}Dark}}}}"


def emit_styles_xaml(t: Tokens) -> str:
    L: list[str] = [
        '<?xml version="1.0" encoding="UTF-8" ?>',
        f"<!-- {HEADER_NOTE} -->",
        '<ResourceDictionary xmlns="http://schemas.microsoft.com/dotnet/2021/maui"',
        '                    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">',
        "",
        "    <!-- Spacing (4pt grid), sizes, radii, border widths, motion. -->",
    ]
    spacing = t.data["spacing"]
    for step, tok in spacing.items():
        if step.startswith("$"):
            continue
        L.append(f'    <x:Double x:Key="Space{step}">{fmt_num(parse_pt(tok["$value"]))}</x:Double>')
    for name, tok in t.data["radius"].items():
        if name.startswith("$"):
            continue
        v = fmt_num(parse_pt(tok["$value"]))
        L.append(f'    <x:Double x:Key="Radius{pascal(name)}">{v}</x:Double>')
        L.append(f'    <CornerRadius x:Key="CornerRadius{pascal(name)}">{v}</CornerRadius>')
    for name, tok in t.data["borderWidth"].items():
        if name.startswith("$"):
            continue
        L.append(f'    <x:Double x:Key="BorderWidth{pascal(name)}">{fmt_num(parse_pt(tok["$value"]))}</x:Double>')
    size = t.data["size"]
    for name, tok in size.items():
        if name.startswith("$") or "$value" not in tok:
            continue
        if name in ("touchTargetMin", "touchTargetAndroid"):
            continue
        L.append(f'    <x:Double x:Key="Size{pascal(name)}">{fmt_num(parse_pt(tok["$value"]))}</x:Double>')
    for name, tok in size.get("icon", {}).items():
        if name.startswith("$"):
            continue
        L.append(f'    <x:Double x:Key="SizeIcon{pascal(name)}">{fmt_num(parse_pt(tok["$value"]))}</x:Double>')
    ios_target = fmt_num(t.dimension("size.touchTargetMin"))
    android_target = fmt_num(t.dimension("size.touchTargetAndroid"))
    L += [
        '    <OnPlatform x:Key="SizeTouchTargetMin" x:TypeArguments="x:Double">',
        f'        <On Platform="iOS" Value="{ios_target}" />',
        f'        <On Platform="Android" Value="{android_target}" />',
        "    </OnPlatform>",
    ]
    for name, tok in t.data["motion"]["duration"].items():
        if name.startswith("$"):
            continue
        L.append(f'    <x:Int32 x:Key="MotionDuration{pascal(name)}">{int(parse_pt(tok["$value"]))}</x:Int32>')

    L += ["", "    <!-- Typography roles (typography.scale.*): Type{Role}{Size} styles for Label. Sizes in pt map 1:1 to MAUI units. -->"]
    for role, sz, spec in t.type_roles():
        key = f"Type{pascal(role)}{pascal(sz)}"
        font_size = parse_pt(spec["fontSize"])
        line_height = parse_pt(spec["lineHeight"]) / font_size
        tracking = parse_pt(spec.get("letterSpacing", "0em")) * font_size
        alias = font_alias(spec["fontFamily"], spec["fontWeight"])
        L.append(f'    <Style x:Key="{key}" TargetType="Label">')
        if alias:
            L.append(f'        <Setter Property="FontFamily" Value="{alias}" />')
        L.append(f'        <Setter Property="FontSize" Value="{fmt_num(font_size)}" />')
        L.append(f'        <Setter Property="LineHeight" Value="{fmt_num(round(line_height, 3))}" />')
        if abs(tracking) > 0.001:
            L.append(f'        <Setter Property="CharacterSpacing" Value="{fmt_num(round(tracking, 2))}" />')
        L.append('        <Setter Property="FontAutoScalingEnabled" Value="True" />')
        L.append(f'        <Setter Property="TextColor" Value="{theme_binding("TextPrimary")}" />')
        L.append("    </Style>")

    body_size = fmt_num(parse_pt(t.node("typography.scale.body.md")["$value"]["fontSize"]))
    radius_md = fmt_num(t.dimension("radius.md"))
    radius_lg = fmt_num(t.dimension("radius.lg"))
    L += [
        "",
        "    <!-- Implicit base styles: surfaces, text, controls. Component styles (WordCard, QuizOption, ...) live in Controls/. -->",
        '    <Style TargetType="Page" ApplyToDerivedTypes="True">',
        f'        <Setter Property="BackgroundColor" Value="{theme_binding("SurfaceDefault")}" />',
        "    </Style>",
        '    <Style TargetType="Shell" ApplyToDerivedTypes="True">',
        f'        <Setter Property="Shell.BackgroundColor" Value="{theme_binding("SurfaceDefault")}" />',
        f'        <Setter Property="Shell.ForegroundColor" Value="{theme_binding("TextPrimary")}" />',
        f'        <Setter Property="Shell.TitleColor" Value="{theme_binding("TextPrimary")}" />',
        f'        <Setter Property="Shell.TabBarBackgroundColor" Value="{theme_binding("SurfaceElevated")}" />',
        f'        <Setter Property="Shell.TabBarForegroundColor" Value="{theme_binding("BrandPrimary")}" />',
        f'        <Setter Property="Shell.TabBarTitleColor" Value="{theme_binding("BrandPrimary")}" />',
        f'        <Setter Property="Shell.TabBarUnselectedColor" Value="{theme_binding("TextTertiary")}" />',
        '        <Setter Property="Shell.NavBarHasShadow" Value="False" />',
        "    </Style>",
        '    <Style TargetType="Label">',
        '        <Setter Property="FontFamily" Value="FixelText" />',
        f'        <Setter Property="FontSize" Value="{body_size}" />',
        '        <Setter Property="FontAutoScalingEnabled" Value="True" />',
        f'        <Setter Property="TextColor" Value="{theme_binding("TextPrimary")}" />',
        "    </Style>",
        '    <Style x:Key="ButtonPrimary" TargetType="Button">',
        '        <Setter Property="FontFamily" Value="FixelTextSemiBold" />',
        f'        <Setter Property="FontSize" Value="{body_size}" />',
        f'        <Setter Property="TextColor" Value="{theme_binding("BrandOnPrimary")}" />',
        f'        <Setter Property="BackgroundColor" Value="{theme_binding("BrandPrimary")}" />',
        f'        <Setter Property="CornerRadius" Value="{radius_md}" />',
        '        <Setter Property="Padding" Value="20,12" />',
        '        <Setter Property="MinimumHeightRequest" Value="{StaticResource SizeTouchTargetMin}" />',
        '        <Setter Property="VisualStateManager.VisualStateGroups">',
        "            <VisualStateGroupList>",
        '                <VisualStateGroup x:Name="CommonStates">',
        '                    <VisualState x:Name="Normal" />',
        '                    <VisualState x:Name="Pressed">',
        '                        <VisualState.Setters>',
        '                            <Setter Property="Scale" Value="0.97" />',
        "                        </VisualState.Setters>",
        "                    </VisualState>",
        '                    <VisualState x:Name="Disabled">',
        '                        <VisualState.Setters>',
        f'                            <Setter Property="BackgroundColor" Value="{theme_binding("SurfaceSunken")}" />',
        f'                            <Setter Property="TextColor" Value="{theme_binding("TextDisabled")}" />',
        "                        </VisualState.Setters>",
        "                    </VisualState>",
        "                </VisualStateGroup>",
        "            </VisualStateGroupList>",
        "        </Setter>",
        "    </Style>",
        '    <Style TargetType="Button" BasedOn="{StaticResource ButtonPrimary}" />',
        '    <Style x:Key="ButtonSecondary" TargetType="Button" BasedOn="{StaticResource ButtonPrimary}">',
        '        <Setter Property="BackgroundColor" Value="Transparent" />',
        f'        <Setter Property="TextColor" Value="{theme_binding("BrandPrimary")}" />',
        f'        <Setter Property="BorderColor" Value="{theme_binding("BorderStrong")}" />',
        '        <Setter Property="BorderWidth" Value="{StaticResource BorderWidthHairline}" />',
        "    </Style>",
        '    <Style TargetType="Entry">',
        '        <Setter Property="FontFamily" Value="FixelText" />',
        f'        <Setter Property="FontSize" Value="{body_size}" />',
        f'        <Setter Property="TextColor" Value="{theme_binding("TextPrimary")}" />',
        f'        <Setter Property="PlaceholderColor" Value="{theme_binding("TextTertiary")}" />',
        '        <Setter Property="BackgroundColor" Value="Transparent" />',
        '        <Setter Property="MinimumHeightRequest" Value="{StaticResource SizeTouchTargetMin}" />',
        "    </Style>",
        '    <Style TargetType="ActivityIndicator">',
        f'        <Setter Property="Color" Value="{theme_binding("BrandPrimary")}" />',
        "    </Style>",
        '    <Style TargetType="Border">',
        f'        <Setter Property="Stroke" Value="{theme_binding("BorderDefault")}" />',
        '        <Setter Property="StrokeThickness" Value="{StaticResource BorderWidthHairline}" />',
        f'        <Setter Property="BackgroundColor" Value="{theme_binding("SurfaceElevated")}" />',
        '        <Setter Property="StrokeShape">',
        f'            <RoundRectangle CornerRadius="{radius_lg}" />',
        "        </Setter>",
        "    </Style>",
        '    <Style TargetType="BoxView">',
        f'        <Setter Property="Color" Value="{theme_binding("BorderDefault")}" />',
        "    </Style>",
        "",
        "</ResourceDictionary>",
        "",
    ]
    return "\n".join(L)


def emit_widget_theme_xaml(name: str, theme: dict[str, dict[str, str]]) -> str:
    lines = [
        '<?xml version="1.0" encoding="UTF-8" ?>',
        f"<!-- {HEADER_NOTE} Widget theme '{name}' (color.theme.widget.{name}): merge after Colors.xaml to override ColorWidget* keys. -->",
        '<ResourceDictionary xmlns="http://schemas.microsoft.com/dotnet/2021/maui"',
        '                    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml">',
    ]
    for key in theme["light"]:
        lines.append(f'    <Color x:Key="ColorWidget{pascal(key)}Light">{to_argb(theme["light"][key])}</Color>')
        if key in theme["dark"]:
            lines.append(f'    <Color x:Key="ColorWidget{pascal(key)}Dark">{to_argb(theme["dark"][key])}</Color>')
    lines += ["</ResourceDictionary>", ""]
    return "\n".join(lines)


def emit_android_colors(sem: dict[str, dict[str, str]]) -> str:
    lines = ['<?xml version="1.0" encoding="utf-8"?>', f"<!-- {HEADER_NOTE} -->", "<resources>"]
    for group, items in sem.items():
        for name, hex_value in items.items():
            lines.append(f'    <color name="burdock_{snake(group)}_{snake(name)}">{to_argb(hex_value)}</color>')
    lines += ["</resources>", ""]
    return "\n".join(lines)


def emit_android_dimens(t: Tokens) -> str:
    lines = ['<?xml version="1.0" encoding="utf-8"?>', f"<!-- {HEADER_NOTE} -->", "<resources>"]
    for step, tok in t.data["spacing"].items():
        if not step.startswith("$"):
            lines.append(f'    <dimen name="burdock_space_{step}">{fmt_num(parse_pt(tok["$value"]))}dp</dimen>')
    for name, tok in t.data["radius"].items():
        if not name.startswith("$"):
            lines.append(f'    <dimen name="burdock_radius_{snake(name)}">{fmt_num(parse_pt(tok["$value"]))}dp</dimen>')
    for name, tok in t.data["size"]["icon"].items():
        if not name.startswith("$"):
            lines.append(f'    <dimen name="burdock_icon_{snake(name)}">{fmt_num(parse_pt(tok["$value"]))}dp</dimen>')
    lines.append(f'    <dimen name="burdock_touch_target_min">{fmt_num(t.dimension("size.touchTargetAndroid"))}dp</dimen>')
    lines += ["</resources>", ""]
    return "\n".join(lines)


def emit_swift(t: Tokens) -> str:
    light, dark = t.semantic("light"), t.semantic("dark")
    L = [
        f"// {HEADER_NOTE}",
        "// Consumed by the WidgetKit/ActivityKit extension; the app itself uses the XAML resources.",
        "import Foundation",
        "",
        "public enum BurdockTokens {",
        "    /// Hex strings (#RRGGBB or #RRGGBBAA) for light and dark appearance; map with Color(hex:) in the extension.",
        "    public struct DynamicColor: Sendable { public let light: String; public let dark: String }",
        "",
        "    public enum Colors {",
    ]
    for group in light:
        for name in light[group]:
            ident = group + pascal(name)
            L.append(f'        public static let {ident} = DynamicColor(light: "{light[group][name]}", dark: "{dark[group][name]}")')
    L += ["    }", "", "    public enum WidgetThemes {"]
    for theme, modes in t.widget_themes().items():
        L.append(f"        public static let {theme}: [String: DynamicColor] = [")
        for key in modes["light"]:
            L.append(f'            "{key}": DynamicColor(light: "{modes["light"][key]}", dark: "{modes["dark"].get(key, modes["light"][key])}"),')
        L.append("        ]")
    L += ["    }", "", "    public enum Spacing {"]
    for step, tok in t.data["spacing"].items():
        if not step.startswith("$"):
            L.append(f"        public static let s{step}: Double = {fmt_num(parse_pt(tok['$value']))}")
    L += ["    }", "", "    public enum Radius {"]
    for name, tok in t.data["radius"].items():
        if not name.startswith("$"):
            L.append(f"        public static let {name}: Double = {fmt_num(parse_pt(tok['$value']))}")
    L += ["    }", "", "    public struct TextStyle: Sendable { public let family: String?; public let size: Double; public let lineHeight: Double; public let weight: Int; public let tracking: Double }", "", "    public enum Type {"]
    for role, sz, spec in t.type_roles():
        fam = spec["fontFamily"][0] if isinstance(spec["fontFamily"], list) else spec["fontFamily"]
        family = "nil" if not str(fam).startswith("Fixel") else f'"{fam}"'
        L.append(
            f"        public static let {role}{pascal(sz)} = TextStyle(family: {family}, size: {fmt_num(parse_pt(spec['fontSize']))}, "
            f"lineHeight: {fmt_num(parse_pt(spec['lineHeight']))}, weight: {int(spec['fontWeight'])}, tracking: {fmt_num(parse_pt(spec.get('letterSpacing', '0em')))})"
        )
    L += ["    }", "}", ""]
    return "\n".join(L)


# ----------------------------------------------------------------------------- outputs


def planned_outputs(t: Tokens, which: set[str]) -> dict[Path, str]:
    out: dict[Path, str] = {}
    if "xaml" in which:
        out[STYLES_DIR / "Colors.xaml"] = emit_colors_xaml(t)
        out[STYLES_DIR / "Styles.xaml"] = emit_styles_xaml(t)
        for name, theme in t.widget_themes().items():
            out[STYLES_DIR / "WidgetThemes" / f"{pascal(name)}.xaml"] = emit_widget_theme_xaml(name, theme)
    if "android" in which:
        out[ANDROID_RES / "values" / "colors_tokens.xml"] = emit_android_colors(t.semantic("light"))
        out[ANDROID_RES / "values-night" / "colors_tokens.xml"] = emit_android_colors(t.semantic("dark"))
        out[ANDROID_RES / "values" / "dimens_tokens.xml"] = emit_android_dimens(t)
    if "swift" in which:
        out[SWIFT_DIR / "DesignTokens.swift"] = emit_swift(t)
    return out


def write_outputs(outputs: dict[Path, str]) -> None:
    for path, content in outputs.items():
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8", newline="\n")
        print(f"wrote {path.relative_to(REPO).as_posix()}")


# ----------------------------------------------------------------------------- checks


def run_checks(t: Tokens) -> int:
    failures: list[str] = []
    checked = 0

    # 1. Every alias resolves and every recorded "resolved" extension matches.
    def walk(node, path):
        nonlocal checked
        if isinstance(node, dict):
            if "$value" in node:
                try:
                    resolved = t.resolve(node["$value"])
                except (KeyError, ValueError) as exc:
                    failures.append(f"alias: {path}: {exc}")
                    return
                recorded = node.get("$extensions", {}).get("org.burdock.tokens", {}).get("resolved")
                if recorded and isinstance(resolved, str) and recorded.upper() != resolved.upper():
                    failures.append(f"resolved mismatch: {path}: recorded {recorded}, actual {resolved}")
                checked += 1
            for k, v in node.items():
                if not k.startswith("$"):
                    walk(v, f"{path}.{k}" if path else k)

    walk(t.data, "")
    print(f"aliases: {checked} tokens resolved")

    # 2. WCAG 2.2 contrast on the semantic sets and on each widget theme.
    total_pairs = 0
    aaa = 0
    for mode in ("light", "dark"):
        sem = t.semantic(mode)
        for fg, bg, req in contrast_pairs(sem):
            g1, n1 = fg.split("."); g2, n2 = bg.split(".")
            ratio = contrast(sem[g1][n1], sem[g2][n2])
            total_pairs += 1
            aaa += ratio >= 7
            if ratio < req:
                failures.append(f"contrast {mode}: {fg} on {bg} = {ratio:.2f} < {req}")
        for theme, modes in t.widget_themes().items():
            widget = dict(sem["widget"]); widget.update(modes[mode])
            for fg, bg, req in [("word", "background", 4.5), ("ipa", "background", 4.5), ("translation", "background", 4.5),
                                ("example", "background", 4.5), ("chipText", "chipBackground", 4.5), ("onButton", "button", 4.5),
                                ("onBadgeFree", "badgeFree", 4.5), ("onAccent", "accent", 4.5)]:
                if fg in widget and bg in widget:
                    ratio = contrast(widget[fg], widget[bg])
                    total_pairs += 1
                    if ratio < req:
                        failures.append(f"contrast {mode}/theme {theme}: widget.{fg} on widget.{bg} = {ratio:.2f} < {req}")
    print(f"contrast: {total_pairs} pairs checked, {aaa} reach 7:1")

    # 3. No hard-coded hex colours in XAML outside the generated style files (spec 02 NFR-02-6).
    hex_re = re.compile(r"#[0-9A-Fa-f]{6,8}\b")
    for xaml in (REPO / "src" / "mobile").rglob("*.xaml"):
        rel = xaml.relative_to(REPO).as_posix()
        if "/Resources/Styles/" in rel or "/bin/" in rel or "/obj/" in rel:
            continue
        for i, line in enumerate(xaml.read_text(encoding="utf-8").splitlines(), 1):
            if hex_re.search(line):
                failures.append(f"hex in XAML: {rel}:{i}: use a token resource instead")

    # 4. Generated files are up to date.
    for path, content in planned_outputs(t, {"xaml", "android", "swift"}).items():
        rel = path.relative_to(REPO).as_posix()
        if not path.exists():
            failures.append(f"drift: {rel} is missing (run generate.py --out all)")
        elif path.read_text(encoding="utf-8") != content:
            failures.append(f"drift: {rel} differs from tokens (run generate.py --out all)")

    if failures:
        print(f"\nFAILED: {len(failures)} problem(s)")
        for f in failures:
            print("  - " + f)
        return 1
    print("\nOK: tokens valid, contrast passes, XAML clean, generated files current")
    return 0


# ----------------------------------------------------------------------------- main


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="Burdock design-token generator")
    parser.add_argument("--check", action="store_true", help="validate tokens, contrast, XAML hex lint and generated-file drift")
    parser.add_argument("--out", choices=["xaml", "android", "swift", "all"], help="emit platform resources")
    parser.add_argument("--seed", action="store_true", help="re-run the OKLCH ramp seed that rewrites tokens.json")
    args = parser.parse_args(argv)

    if args.seed:
        import runpy
        runpy.run_path(str(HERE / "_seed_ramps.py"), run_name="__main__")

    if not args.check and not args.out and not args.seed:
        parser.print_help()
        return 2

    t = Tokens(load_tokens())
    if args.out:
        which = {"xaml", "android", "swift"} if args.out == "all" else {args.out}
        write_outputs(planned_outputs(t, which))
    if args.check:
        return run_checks(t)
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
