// GENERATED from design/tokens/tokens.json by design/tokens/generate.py. Do not edit by hand; change the tokens and regenerate.
// Consumed by the WidgetKit/ActivityKit extension; the app itself uses the XAML resources.
import Foundation

public enum BurdockTokens {
    /// Hex strings (#RRGGBB or #RRGGBBAA) for light and dark appearance; map with Color(hex:) in the extension.
    public struct DynamicColor: Sendable { public let light: String; public let dark: String }

    public enum Colors {
        public static let brandPrimary = DynamicColor(light: "#6D49EB", dark: "#9A8EFE")
        public static let brandPrimaryEdge = DynamicColor(light: "#5137AE", dark: "#6D49EB")
        public static let brandOnPrimary = DynamicColor(light: "#FFFFFF", dark: "#15131F")
        public static let brandPrimaryVivid = DynamicColor(light: "#7C5CFF", dark: "#7C5CFF")
        public static let brandPrimaryContainer = DynamicColor(light: "#ECECFF", dark: "#2C2160")
        public static let brandOnPrimaryContainer = DynamicColor(light: "#3E2C87", dark: "#D5D4FF")
        public static let brandPrimaryContainerEdge = DynamicColor(light: "#D5D4FF", dark: "#1E183E")
        public static let brandAccent = DynamicColor(light: "#B4F461", dark: "#B4F461")
        public static let brandOnAccent = DynamicColor(light: "#162404", dark: "#162404")
        public static let brandAccentText = DynamicColor(light: "#4F7700", dark: "#C4FB84")
        public static let brandAccentEdge = DynamicColor(light: "#689C00", dark: "#689C00")
        public static let surfaceDefault = DynamicColor(light: "#F7F6FC", dark: "#15131F")
        public static let surfaceElevated = DynamicColor(light: "#FFFFFF", dark: "#25242D")
        public static let surfaceElevatedEdge = DynamicColor(light: "#DBD9E3", dark: "#33313B")
        public static let surfaceSunken = DynamicColor(light: "#EDEBF5", dark: "#0C0B14")
        public static let surfaceOverlay = DynamicColor(light: "#0C0B148F", dark: "#000000A3")
        public static let surfaceBrandTint = DynamicColor(light: "#F9F9FF", dark: "#1E183E")
        public static let textPrimary = DynamicColor(light: "#15131F", dark: "#F7F6FC")
        public static let textSecondary = DynamicColor(light: "#585661", dark: "#BAB9C3")
        public static let textTertiary = DynamicColor(light: "#6A6873", dark: "#8C8B95")
        public static let textInverse = DynamicColor(light: "#F7F6FC", dark: "#15131F")
        public static let textLink = DynamicColor(light: "#5137AE", dark: "#B8B4FE")
        public static let textDisabled = DynamicColor(light: "#8C8B95", dark: "#585661")
        public static let borderDefault = DynamicColor(light: "#DBD9E3", dark: "#33313B")
        public static let borderStrong = DynamicColor(light: "#8C8B95", dark: "#8C8B95")
        public static let borderFocus = DynamicColor(light: "#6D49EB", dark: "#B8B4FE")
        public static let feedbackSuccess = DynamicColor(light: "#4F7700", dark: "#C4FB84")
        public static let feedbackSuccessEdge = DynamicColor(light: "#3E5C0E", dark: "#92CB42")
        public static let feedbackSuccessContainer = DynamicColor(light: "#EAFFD5", dark: "#283D07")
        public static let feedbackOnSuccessContainer = DynamicColor(light: "#3E5C0E", dark: "#D7FFAD")
        public static let feedbackError = DynamicColor(light: "#B74244", dark: "#FFA29E")
        public static let feedbackErrorEdge = DynamicColor(light: "#913334", dark: "#DC5556")
        public static let feedbackErrorContainer = DynamicColor(light: "#FFE9E7", dark: "#501C1C")
        public static let feedbackOnErrorContainer = DynamicColor(light: "#702727", dark: "#FFCBC7")
        public static let feedbackWarning = DynamicColor(light: "#866302", dark: "#FFD889")
        public static let feedbackWarningContainer = DynamicColor(light: "#FFF3DC", dark: "#453100")
        public static let feedbackOnWarningContainer = DynamicColor(light: "#644900", dark: "#FFE8BA")
        public static let feedbackInfo = DynamicColor(light: "#126B83", dark: "#7CDDFF")
        public static let feedbackInfoContainer = DynamicColor(light: "#DFF6FF", dark: "#0A3745")
        public static let feedbackOnInfoContainer = DynamicColor(light: "#0D5064", dark: "#B7ECFF")
        public static let rewardGold = DynamicColor(light: "#FFC53D", dark: "#FFC53D")
        public static let rewardGoldText = DynamicColor(light: "#644900", dark: "#FFD889")
        public static let rewardGoldContainer = DynamicColor(light: "#FFF3DC", dark: "#453100")
        public static let rewardCurrency = DynamicColor(light: "#689C00", dark: "#B4F461")
        public static let rewardCurrencyText = DynamicColor(light: "#3E5C0E", dark: "#C4FB84")
        public static let rewardCurrencyContainer = DynamicColor(light: "#EAFFD5", dark: "#283D07")
        public static let rewardHeart = DynamicColor(light: "#FF6B6B", dark: "#FF6B6B")
        public static let streakActive = DynamicColor(light: "#D7A21A", dark: "#FFC53D")
        public static let streakText = DynamicColor(light: "#644900", dark: "#FFD889")
        public static let streakContainer = DynamicColor(light: "#FFF3DC", dark: "#453100")
        public static let streakFrozen = DynamicColor(light: "#1F89A8", dark: "#4CC9F0")
        public static let streakFrozenText = DynamicColor(light: "#0D5064", dark: "#7CDDFF")
        public static let streakInactive = DynamicColor(light: "#BAB9C3", dark: "#45434E")
        public static let quizCorrect = DynamicColor(light: "#4F7700", dark: "#C4FB84")
        public static let quizCorrectContainer = DynamicColor(light: "#EAFFD5", dark: "#283D07")
        public static let quizCorrectBorder = DynamicColor(light: "#689C00", dark: "#92CB42")
        public static let quizIncorrect = DynamicColor(light: "#B74244", dark: "#FFA29E")
        public static let quizIncorrectContainer = DynamicColor(light: "#FFE9E7", dark: "#501C1C")
        public static let quizIncorrectBorder = DynamicColor(light: "#DC5556", dark: "#DC5556")
        public static let quizSelected = DynamicColor(light: "#5137AE", dark: "#D5D4FF")
        public static let quizSelectedContainer = DynamicColor(light: "#ECECFF", dark: "#2C2160")
        public static let quizSelectedBorder = DynamicColor(light: "#6D49EB", dark: "#9A8EFE")
        public static let widgetBackground = DynamicColor(light: "#6D49EB", dark: "#2C2160")
        public static let widgetBackgroundEnd = DynamicColor(light: "#3E2C87", dark: "#15131F")
        public static let widgetWord = DynamicColor(light: "#FFFFFF", dark: "#F7F6FC")
        public static let widgetIpa = DynamicColor(light: "#ECECFF", dark: "#D5D4FF")
        public static let widgetTranslation = DynamicColor(light: "#ECECFF", dark: "#D5D4FF")
        public static let widgetExample = DynamicColor(light: "#ECECFF", dark: "#B8B4FE")
        public static let widgetAccent = DynamicColor(light: "#B4F461", dark: "#B4F461")
        public static let widgetOnAccent = DynamicColor(light: "#162404", dark: "#162404")
        public static let widgetChipBackground = DynamicColor(light: "#3E2C87", dark: "#3E2C87")
        public static let widgetChipText = DynamicColor(light: "#ECECFF", dark: "#ECECFF")
        public static let widgetButton = DynamicColor(light: "#FFFFFF", dark: "#B8B4FE")
        public static let widgetOnButton = DynamicColor(light: "#5137AE", dark: "#1E183E")
        public static let widgetDivider = DynamicColor(light: "#7C5CFF", dark: "#3E2C87")
        public static let widgetBadgeFree = DynamicColor(light: "#3E2C87", dark: "#3E2C87")
        public static let widgetOnBadgeFree = DynamicColor(light: "#ECECFF", dark: "#ECECFF")
        public static let topicCommunication = DynamicColor(light: "#ECECFF", dark: "#2C2160")
        public static let topicOnCommunication = DynamicColor(light: "#3E2C87", dark: "#D5D4FF")
        public static let topicTravel = DynamicColor(light: "#DFF6FF", dark: "#0A3745")
        public static let topicOnTravel = DynamicColor(light: "#0D5064", dark: "#B7ECFF")
        public static let topicWork = DynamicColor(light: "#FFF3DC", dark: "#453100")
        public static let topicOnWork = DynamicColor(light: "#644900", dark: "#FFE8BA")
        public static let topicIt = DynamicColor(light: "#D5D4FF", dark: "#1E183E")
        public static let topicOnIt = DynamicColor(light: "#2C2160", dark: "#B8B4FE")
        public static let topicFood = DynamicColor(light: "#FFE9E7", dark: "#501C1C")
        public static let topicOnFood = DynamicColor(light: "#702727", dark: "#FFCBC7")
        public static let topicHealth = DynamicColor(light: "#EAFFD5", dark: "#283D07")
        public static let topicOnHealth = DynamicColor(light: "#3E5C0E", dark: "#D7FFAD")
        public static let topicCity = DynamicColor(light: "#B7ECFF", dark: "#08232C")
        public static let topicOnCity = DynamicColor(light: "#0A3745", dark: "#7CDDFF")
        public static let topicShopping = DynamicColor(light: "#FFE8BA", dark: "#2A1D01")
        public static let topicOnShopping = DynamicColor(light: "#453100", dark: "#FFD889")
        public static let topicMilitary = DynamicColor(light: "#D7FFAD", dark: "#162404")
        public static let topicOnMilitary = DynamicColor(light: "#283D07", dark: "#C4FB84")
    }

    public enum WidgetThemes {
        public static let tsvit: [String: DynamicColor] = [
        ]
        public static let luh: [String: DynamicColor] = [
            "background": DynamicColor(light: "#EAFFD5", dark: "#162404"),
            "backgroundEnd": DynamicColor(light: "#D7FFAD", dark: "#15131F"),
            "word": DynamicColor(light: "#162404", dark: "#EAFFD5"),
            "ipa": DynamicColor(light: "#3E5C0E", dark: "#C4FB84"),
            "translation": DynamicColor(light: "#3E5C0E", dark: "#C4FB84"),
            "example": DynamicColor(light: "#3E5C0E", dark: "#C4FB84"),
            "accent": DynamicColor(light: "#6D49EB", dark: "#B4F461"),
            "onAccent": DynamicColor(light: "#FFFFFF", dark: "#162404"),
            "chipBackground": DynamicColor(light: "#C4FB84", dark: "#283D07"),
            "chipText": DynamicColor(light: "#283D07", dark: "#D7FFAD"),
            "button": DynamicColor(light: "#162404", dark: "#B4F461"),
            "onButton": DynamicColor(light: "#EAFFD5", dark: "#162404"),
            "divider": DynamicColor(light: "#C4FB84", dark: "#283D07"),
            "badgeFree": DynamicColor(light: "#C4FB84", dark: "#283D07"),
            "onBadgeFree": DynamicColor(light: "#283D07", dark: "#D7FFAD"),
        ]
        public static let nich: [String: DynamicColor] = [
            "background": DynamicColor(light: "#15131F", dark: "#0C0B14"),
            "backgroundEnd": DynamicColor(light: "#1E183E", dark: "#1E183E"),
            "word": DynamicColor(light: "#F7F6FC", dark: "#F7F6FC"),
            "ipa": DynamicColor(light: "#BAB9C3", dark: "#BAB9C3"),
            "translation": DynamicColor(light: "#BAB9C3", dark: "#BAB9C3"),
            "example": DynamicColor(light: "#8C8B95", dark: "#8C8B95"),
            "accent": DynamicColor(light: "#7CDDFF", dark: "#7CDDFF"),
            "onAccent": DynamicColor(light: "#08232C", dark: "#08232C"),
            "chipBackground": DynamicColor(light: "#33313B", dark: "#33313B"),
            "chipText": DynamicColor(light: "#DBD9E3", dark: "#DBD9E3"),
            "button": DynamicColor(light: "#7CDDFF", dark: "#7CDDFF"),
            "onButton": DynamicColor(light: "#08232C", dark: "#08232C"),
            "divider": DynamicColor(light: "#33313B", dark: "#33313B"),
            "badgeFree": DynamicColor(light: "#33313B", dark: "#33313B"),
            "onBadgeFree": DynamicColor(light: "#DBD9E3", dark: "#DBD9E3"),
        ]
        public static let sontse: [String: DynamicColor] = [
            "background": DynamicColor(light: "#FFE8BA", dark: "#2A1D01"),
            "backgroundEnd": DynamicColor(light: "#FFC53D", dark: "#15131F"),
            "word": DynamicColor(light: "#2A1D01", dark: "#FFF3DC"),
            "ipa": DynamicColor(light: "#644900", dark: "#FFD889"),
            "translation": DynamicColor(light: "#644900", dark: "#FFD889"),
            "example": DynamicColor(light: "#644900", dark: "#FFD889"),
            "accent": DynamicColor(light: "#B74244", dark: "#FFC53D"),
            "onAccent": DynamicColor(light: "#FFFFFF", dark: "#2A1D01"),
            "chipBackground": DynamicColor(light: "#FFC53D", dark: "#453100"),
            "chipText": DynamicColor(light: "#453100", dark: "#FFE8BA"),
            "button": DynamicColor(light: "#2A1D01", dark: "#FFC53D"),
            "onButton": DynamicColor(light: "#FFF3DC", dark: "#2A1D01"),
            "divider": DynamicColor(light: "#FFC53D", dark: "#453100"),
            "badgeFree": DynamicColor(light: "#FFC53D", dark: "#453100"),
            "onBadgeFree": DynamicColor(light: "#453100", dark: "#FFE8BA"),
        ]
    }

    public enum Spacing {
        public static let s1: Double = 4
        public static let s2: Double = 8
        public static let s3: Double = 12
        public static let s4: Double = 16
        public static let s5: Double = 20
        public static let s6: Double = 24
        public static let s7: Double = 32
        public static let s8: Double = 40
        public static let s9: Double = 48
        public static let s10: Double = 56
        public static let s11: Double = 64
        public static let s12: Double = 80
    }

    public enum Radius {
        public static let sm: Double = 8
        public static let md: Double = 12
        public static let lg: Double = 16
        public static let xl: Double = 24
        public static let full: Double = 999
    }

    public struct TextStyle: Sendable { public let family: String?; public let size: Double; public let lineHeight: Double; public let weight: Int; public let tracking: Double }

    public enum Type {
        public static let displayLg = TextStyle(family: "Unbounded", size: 40, lineHeight: 46, weight: 700, tracking: -0.02)
        public static let displayMd = TextStyle(family: "Unbounded", size: 32, lineHeight: 38, weight: 700, tracking: -0.015)
        public static let headlineLg = TextStyle(family: "Unbounded", size: 28, lineHeight: 34, weight: 600, tracking: -0.01)
        public static let headlineMd = TextStyle(family: "Unbounded", size: 24, lineHeight: 30, weight: 600, tracking: -0.005)
        public static let headlineSm = TextStyle(family: "Rubik", size: 20, lineHeight: 26, weight: 600, tracking: 0)
        public static let titleLg = TextStyle(family: "Rubik", size: 18, lineHeight: 24, weight: 600, tracking: 0)
        public static let titleMd = TextStyle(family: "Rubik", size: 16, lineHeight: 22, weight: 600, tracking: 0)
        public static let titleSm = TextStyle(family: "Rubik", size: 14, lineHeight: 20, weight: 600, tracking: 0.005)
        public static let bodyLg = TextStyle(family: "Rubik", size: 17, lineHeight: 24, weight: 400, tracking: 0)
        public static let bodyMd = TextStyle(family: "Rubik", size: 15, lineHeight: 22, weight: 400, tracking: 0)
        public static let bodySm = TextStyle(family: "Rubik", size: 13, lineHeight: 18, weight: 400, tracking: 0.005)
        public static let labelLg = TextStyle(family: "Rubik", size: 16, lineHeight: 20, weight: 600, tracking: 0.005)
        public static let labelMd = TextStyle(family: "Rubik", size: 14, lineHeight: 18, weight: 500, tracking: 0.01)
        public static let labelSm = TextStyle(family: "Rubik", size: 12, lineHeight: 16, weight: 500, tracking: 0.02)
        public static let wordHero = TextStyle(family: "Unbounded", size: 34, lineHeight: 40, weight: 700, tracking: -0.01)
        public static let wordIpa = TextStyle(family: nil, size: 15, lineHeight: 20, weight: 400, tracking: 0)
        public static let numericStat = TextStyle(family: "Rubik", size: 28, lineHeight: 32, weight: 700, tracking: 0)
    }
}
