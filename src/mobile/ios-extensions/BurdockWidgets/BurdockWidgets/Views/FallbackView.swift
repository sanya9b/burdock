import SwiftUI
import WidgetKit

/// «Реп’яшок спить» (FR-04-8, spec 04 §3.3.2): the timeline is over or missing; a tap opens «Сьогодні». No pressure, no streak talk.
struct FallbackView: View {
    let model: WordWidgetModel
    let family: WidgetFamily

    var body: some View {
        content.widgetURL(model.url(for: family))
    }

    @ViewBuilder
    private var content: some View {
        switch family {
        case .accessoryInline:
            Label {
                Text("widget.fallback.inline")
            } icon: {
                Image(systemName: "moon.zzz")
            }
        case .accessoryCircular:
            ZStack {
                AccessoryWidgetBackground()
                BurrGlyph(size: 30, color: .primary)
            }
            .accessibilityLabel(Text("widget.fallback.title"))
        case .accessoryRectangular:
            HStack(spacing: WidgetMetrics.s2) {
                Image(systemName: "moon.zzz")
                VStack(alignment: .leading, spacing: 2) {
                    Text("widget.fallback.title").font(.headline).lineLimit(1)
                    Text("widget.fallback.body").font(.caption).lineLimit(2)
                }
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .leading)
        default:
            let small = family == .systemSmall
            VStack(alignment: .leading, spacing: WidgetMetrics.s2) {
                BurrGlyph(size: small ? 40 : 56, color: model.palette.word, eyeColor: model.palette.background, eyes: .asleep)
                Spacer(minLength: 0)
                Text("widget.fallback.title")
                    .font(WidgetFont.word(small ? 15 : 20, relativeTo: .headline))
                    .foregroundStyle(model.palette.word)
                    .lineLimit(2)
                    .minimumScaleFactor(0.8)
                    .widgetAccentable()
                Text("widget.fallback.body")
                    .font(WidgetFont.label(small ? 12 : 14, relativeTo: .footnote))
                    .foregroundStyle(model.palette.translation)
                    .lineLimit(2)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .leading)
        }
    }
}
