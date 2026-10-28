import SwiftUI
import UIKit
import BurdockShared

/// Colours of one widget theme (spec 02 §3.12): the default «Цвіт» comes from `color.widget.*`, the others from `WidgetThemes`.
struct WidgetPalette {
    let background: Color
    let backgroundEnd: Color
    let word: Color
    let ipa: Color
    let translation: Color
    let example: Color
    let accent: Color
    let onAccent: Color
    let chipBackground: Color
    let chipText: Color
    let button: Color
    let onButton: Color
    let divider: Color
    let badgeFree: Color
    let onBadgeFree: Color

    var gradient: LinearGradient {
        LinearGradient(colors: [background, backgroundEnd], startPoint: .topLeading, endPoint: .bottomTrailing)
    }

    static let tsvit = WidgetPalette(
        background: Color(BurdockTokens.Colors.widgetBackground),
        backgroundEnd: Color(BurdockTokens.Colors.widgetBackgroundEnd),
        word: Color(BurdockTokens.Colors.widgetWord),
        ipa: Color(BurdockTokens.Colors.widgetIpa),
        translation: Color(BurdockTokens.Colors.widgetTranslation),
        example: Color(BurdockTokens.Colors.widgetExample),
        accent: Color(BurdockTokens.Colors.widgetAccent),
        onAccent: Color(BurdockTokens.Colors.widgetOnAccent),
        chipBackground: Color(BurdockTokens.Colors.widgetChipBackground),
        chipText: Color(BurdockTokens.Colors.widgetChipText),
        button: Color(BurdockTokens.Colors.widgetButton),
        onButton: Color(BurdockTokens.Colors.widgetOnButton),
        divider: Color(BurdockTokens.Colors.widgetDivider),
        badgeFree: Color(BurdockTokens.Colors.widgetBadgeFree),
        onBadgeFree: Color(BurdockTokens.Colors.widgetOnBadgeFree))

    /// `themeId` of a timeline entry: `burdock.default`/`tsvit` is the default, `luh`/`nich`/`sontse` (optionally prefixed) a named theme.
    static func named(_ themeId: String) -> WidgetPalette {
        let key = themeId.split(separator: ".").last.map(String.init) ?? themeId
        switch key {
        case "luh": return WidgetPalette(theme: BurdockTokens.WidgetThemes.luh) ?? .tsvit
        case "nich": return WidgetPalette(theme: BurdockTokens.WidgetThemes.nich) ?? .tsvit
        case "sontse": return WidgetPalette(theme: BurdockTokens.WidgetThemes.sontse) ?? .tsvit
        default: return .tsvit
        }
    }
}

extension WidgetPalette {
    /// A generated theme dictionary; nil when a slot is missing (the generator guarantees the full set, this guards drift).
    init?(theme: [String: BurdockTokens.DynamicColor]) {
        guard let background = theme["background"], let backgroundEnd = theme["backgroundEnd"], let word = theme["word"],
              let ipa = theme["ipa"], let translation = theme["translation"], let example = theme["example"],
              let accent = theme["accent"], let onAccent = theme["onAccent"], let chipBackground = theme["chipBackground"],
              let chipText = theme["chipText"], let button = theme["button"], let onButton = theme["onButton"],
              let divider = theme["divider"], let badgeFree = theme["badgeFree"], let onBadgeFree = theme["onBadgeFree"] else {
            return nil
        }
        self.init(
            background: Color(background), backgroundEnd: Color(backgroundEnd), word: Color(word), ipa: Color(ipa),
            translation: Color(translation), example: Color(example), accent: Color(accent), onAccent: Color(onAccent),
            chipBackground: Color(chipBackground), chipText: Color(chipText), button: Color(button), onButton: Color(onButton),
            divider: Color(divider), badgeFree: Color(badgeFree), onBadgeFree: Color(onBadgeFree))
    }
}

extension Color {
    /// A light/dark token pair as one dynamic colour that follows the appearance of the widget.
    init(_ token: BurdockTokens.DynamicColor) {
        self.init(uiColor: UIColor { traits in
            UIColor(hex: traits.userInterfaceStyle == .dark ? token.dark : token.light)
        })
    }
}

extension UIColor {
    /// `#RRGGBB` or `#RRGGBBAA` as emitted by design/tokens/generate.py.
    convenience init(hex: String) {
        var value = hex.trimmingCharacters(in: .whitespacesAndNewlines)
        if value.hasPrefix("#") {
            value.removeFirst()
        }
        var number: UInt64 = 0
        Scanner(string: value).scanHexInt64(&number)
        let hasAlpha = value.count == 8
        let red = CGFloat((number >> (hasAlpha ? 24 : 16)) & 0xFF) / 255
        let green = CGFloat((number >> (hasAlpha ? 16 : 8)) & 0xFF) / 255
        let blue = CGFloat((number >> (hasAlpha ? 8 : 0)) & 0xFF) / 255
        let alpha = hasAlpha ? CGFloat(number & 0xFF) / 255 : 1
        self.init(red: red, green: green, blue: blue, alpha: alpha)
    }
}

/// The two embedded faces (ADR 0018): Unbounded Bold for the word, Rubik Medium for labels; everything else is the system font.
enum WidgetFont {
    static func word(_ size: CGFloat, relativeTo style: Font.TextStyle = .title2) -> Font {
        .custom("Unbounded-Bold", size: size, relativeTo: style)
    }

    static func label(_ size: CGFloat, relativeTo style: Font.TextStyle = .body) -> Font {
        .custom("Rubik-Medium", size: size, relativeTo: style)
    }
}

/// Spacing and radius tokens as CGFloat for SwiftUI modifiers.
enum WidgetMetrics {
    static let s1 = CGFloat(BurdockTokens.Spacing.s1)
    static let s2 = CGFloat(BurdockTokens.Spacing.s2)
    static let s3 = CGFloat(BurdockTokens.Spacing.s3)
    static let s4 = CGFloat(BurdockTokens.Spacing.s4)
    static let radiusMd = CGFloat(BurdockTokens.Radius.md)
    static let radiusXl = CGFloat(BurdockTokens.Radius.xl)
    /// Visual size of the «Далі» button and its 44 pt touch target (spec 02 §3.12).
    static let buttonSize: CGFloat = 28
    static let touchTarget: CGFloat = 44
}
