import SwiftUI
import WidgetKit

/// English text for VoiceOver: the language attribute stops the screen reader from spelling Latin letters with Ukrainian phonetics (CLAUDE.md §5.4).
struct EnglishText: View {
    private let text: String

    init(_ text: String) {
        self.text = text
    }

    var body: some View {
        Text(attributed)
    }

    private var attributed: AttributedString {
        var value = AttributedString(text)
        value.languageIdentifier = "en"
        return value
    }
}

/// Category chip: a filled capsule in full colour, an outline when the system paints the widget (accented, vibrant, clear).
struct CategoryChip: View {
    let text: String
    let palette: WidgetPalette
    @Environment(\.widgetRenderingMode) private var renderingMode

    var body: some View {
        Text(text)
            .font(WidgetFont.label(11, relativeTo: .caption2))
            .lineLimit(1)
            .padding(.horizontal, WidgetMetrics.s2)
            .padding(.vertical, 3)
            .foregroundStyle(renderingMode == .fullColor ? palette.chipText : .primary)
            .background {
                if renderingMode == .fullColor {
                    Capsule().fill(palette.chipBackground)
                } else {
                    Capsule().strokeBorder(.primary.opacity(0.6), lineWidth: 1)
                }
            }
    }
}

/// «3/5 сьогодні», or the free-tier pill once the day's pool is used up; counters are private on the lock screen.
struct ProgressBadge: View {
    let model: WordWidgetModel

    var body: some View {
        if let text = model.progressText {
            if model.showsFreeBadge {
                Text(text)
                    .font(WidgetFont.label(11, relativeTo: .caption2))
                    .lineLimit(1)
                    .padding(.horizontal, WidgetMetrics.s2)
                    .padding(.vertical, 3)
                    .foregroundStyle(model.palette.onBadgeFree)
                    .background(Capsule().fill(model.palette.badgeFree))
                    .privacySensitive()
            } else {
                Text(text)
                    .font(.system(size: 12))
                    .lineLimit(1)
                    .foregroundStyle(model.palette.ipa)
                    .privacySensitive()
            }
        }
    }
}

/// «Далі» (spec 02 §3.12): on iOS 17+ an in-process intent, 28 pt circle with a 44 pt touch target; on iOS 16 nothing (a tap opens the app).
struct NextButton: View {
    let palette: WidgetPalette

    var body: some View {
        if #available(iOS 17.0, *) {
            Button(intent: NextWordIntent()) {
                glyph
            }
            .buttonStyle(.plain)
            .accessibilityLabel(Text("widget.next"))
            .widgetAccentable()
        } else {
            EmptyView()
        }
    }

    private var glyph: some View {
        Image(systemName: "arrow.right")
            .font(.system(size: 13, weight: .bold))
            .foregroundStyle(palette.onButton)
            .frame(width: WidgetMetrics.buttonSize, height: WidgetMetrics.buttonSize)
            .background(Circle().fill(palette.button))
            .contentShape(Circle().inset(by: -(WidgetMetrics.touchTarget - WidgetMetrics.buttonSize) / 2))
    }
}

/// «▸ Перевірити» link of systemMedium.
struct QuizLink: View {
    let model: WordWidgetModel

    var body: some View {
        Link(destination: model.quizURL) {
            HStack(spacing: WidgetMetrics.s1) {
                Image(systemName: "checkmark.circle.fill").imageScale(.small)
                Text("widget.quiz")
            }
            .font(WidgetFont.label(13, relativeTo: .footnote))
            .foregroundStyle(model.palette.accent)
            .lineLimit(1)
        }
    }
}
