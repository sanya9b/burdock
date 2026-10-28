import WidgetKit
import BurdockShared

/// Reads the planned timeline from the App Group and hands it to WidgetKit as is (spec 04 §3.2–3.3): no planning here. Every
/// `getTimeline` leaves a marker so the app can tell whether scheduled exposures were ever on a surface (§3.5).
struct WordTimelineProvider: TimelineProvider {
    /// Without a timeline file (fresh install, app not opened yet) the system is asked again in an hour; the app reloads earlier.
    static let retryWithoutTimeline: TimeInterval = 3600

    func placeholder(in context: Context) -> WordTimelineEntry {
        WordTimelineEntry(date: Date(), model: .preview())
    }

    func getSnapshot(in context: Context, completion: @escaping (WordTimelineEntry) -> Void) {
        let now = Date()
        if context.isPreview {
            completion(WordTimelineEntry(date: now, model: .preview()))
            return
        }
        if let store = TimelineStore.appGroup(), let timeline = store.loadTimeline(),
           let slot = TimelineSelection.upcoming(in: timeline, cursor: store.loadCursor(), now: now).first, slot.entry != nil {
            completion(WordTimelineEntry(date: now, model: .make(slot: slot, timeline: timeline)))
            return
        }
        completion(WordTimelineEntry(date: now, model: .preview()))
    }

    func getTimeline(in context: Context, completion: @escaping (Timeline<WordTimelineEntry>) -> Void) {
        let now = Date()
        guard let store = TimelineStore.appGroup(), let timeline = store.loadTimeline(), !timeline.entries.isEmpty else {
            completion(Timeline(entries: [WordTimelineEntry(date: now, model: .fallback())], policy: .after(now.addingTimeInterval(Self.retryWithoutTimeline))))
            return
        }
        store.recordRequest(family: context.family.markerName, now: now)
        let slots = TimelineSelection.upcoming(in: timeline, cursor: store.loadCursor(), now: now)
        let entries = slots.map { WordTimelineEntry(date: $0.date, model: .make(slot: $0, timeline: timeline)) }
        // `.atEnd` is the only system-driven reload (§3.3.2); once the horizon is over there is nothing to ask for until the app writes again.
        completion(Timeline(entries: entries, policy: now < timeline.plannedUntil ? .atEnd : .never))
    }
}
