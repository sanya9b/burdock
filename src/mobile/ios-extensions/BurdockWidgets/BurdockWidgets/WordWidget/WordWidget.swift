import SwiftUI
import WidgetKit
import BurdockShared

struct WordTimelineEntry: TimelineEntry {
    let date: Date
    let model: WordWidgetModel
}

/// The one widget of v1 (spec 04 §3.4): one `StaticConfiguration` under the kind «WordWidget» for all six families; the
/// App Intent configuration of word sets arrives with Entitlements.
struct WordWidget: Widget {
    var body: some WidgetConfiguration {
        StaticConfiguration(kind: WordWidgetKind.value, provider: WordTimelineProvider()) { entry in
            WordWidgetEntryView(entry: entry)
        }
        .configurationDisplayName("widget.displayName")
        .description("widget.description")
        .supportedFamilies([.systemSmall, .systemMedium, .systemLarge, .accessoryRectangular, .accessoryCircular, .accessoryInline])
    }
}

struct WordWidgetEntryView: View {
    @Environment(\.widgetFamily) private var family
    let entry: WordTimelineEntry

    var body: some View {
        WordWidgetView(model: entry.model, family: family)
            .widgetBackground(entry.model.palette, family: family)
    }
}

extension View {
    /// `containerBackground` on iOS 17+ so StandBy, tinted and clear modes can drop it (spec 02 §3.12); a plain gradient with
    /// the system margin on iOS 16. Accessory families never paint a background of their own.
    @ViewBuilder
    func widgetBackground(_ palette: WidgetPalette, family: WidgetFamily) -> some View {
        if #available(iOS 17.0, *) {
            if family.isAccessory {
                containerBackground(for: .widget) { Color.clear }
            } else {
                containerBackground(for: .widget) { palette.gradient }
            }
        } else if family.isAccessory {
            self
        } else {
            padding(WidgetMetrics.s4).background(palette.gradient)
        }
    }
}
