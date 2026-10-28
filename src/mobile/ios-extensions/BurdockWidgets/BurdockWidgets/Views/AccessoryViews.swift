import SwiftUI
import WidgetKit
import BurdockShared

/// Lock screen rectangle: word and translation, IPA when room allows; text and SF Symbols only (vibrant rendering).
struct RectangularWordView: View {
    let model: WordWidgetModel
    @Environment(\.dynamicTypeSize) private var typeSize

    var body: some View {
        VStack(alignment: .leading, spacing: 2) {
            HStack(spacing: WidgetMetrics.s1) {
                Image(systemName: "sparkle").imageScale(.small)
                EnglishText(model.word)
                    .font(.headline.weight(.bold))
                    .lineLimit(1)
                    .minimumScaleFactor(0.7)
                    .widgetAccentable()
            }
            Text(model.translation)
                .font(.system(size: 13))
                .lineLimit(1)
            if let ipa = model.ipa, typeSize < .xLarge {
                Text(ipa)
                    .font(.system(size: 11))
                    .foregroundStyle(.secondary)
                    .lineLimit(1)
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .leading)
        .widgetURL(model.url(for: .accessoryRectangular))
    }
}

/// Lock screen circle: the word when it is 7 letters or fewer, otherwise the burr glyph; never the translation.
struct CircularWordView: View {
    let model: WordWidgetModel

    var body: some View {
        ZStack {
            AccessoryWidgetBackground()
            if let short = WidgetText.circular(model.word) {
                VStack(spacing: 0) {
                    EnglishText(short)
                        .font(.system(size: 13, weight: .bold))
                        .lineLimit(1)
                        .minimumScaleFactor(0.6)
                        .widgetAccentable()
                    Text(verbatim: "EN")
                        .font(.system(size: 9, weight: .semibold))
                        .foregroundStyle(.secondary)
                }
                .padding(.horizontal, WidgetMetrics.s1)
            } else {
                BurrGlyph(size: 30, color: .primary)
            }
        }
        .widgetURL(model.url(for: .accessoryCircular))
        .accessibilityLabel(Text(verbatim: model.word))
    }
}

/// Lock screen inline line: «✦ word — translation», the translation shortened first.
struct InlineWordView: View {
    let model: WordWidgetModel

    var body: some View {
        Label {
            Text(model.translationHidden ? model.word : WidgetText.inline(word: model.word, translation: model.translation))
        } icon: {
            Image(systemName: "sparkle")
        }
        .widgetURL(model.url(for: .accessoryInline))
    }
}
