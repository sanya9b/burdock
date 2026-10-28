import SwiftUI
import WidgetKit
import BurdockShared

/// Content of the «Слово» widget for every family (spec 02 §3.12 mockups, spec 04 §3.4). Backgrounds are applied by the host:
/// `containerBackground` in the extension, a plain gradient in the gallery.
struct WordWidgetView: View {
    let model: WordWidgetModel
    let family: WidgetFamily

    var body: some View {
        if model.isFallback {
            FallbackView(model: model, family: family)
        } else {
            switch family {
            case .systemSmall:
                SmallWordView(model: model)
            case .systemMedium:
                MediumWordView(model: model)
            case .systemLarge, .systemExtraLarge:
                LargeWordView(model: model)
            case .accessoryRectangular:
                RectangularWordView(model: model)
            case .accessoryCircular:
                CircularWordView(model: model)
            case .accessoryInline:
                InlineWordView(model: model)
            @unknown default:
                SmallWordView(model: model)
            }
        }
    }
}

/// 2×2: chip, word, IPA, translation, counter and «Далі» (word ≥ 22 pt, scales to 70 % before truncating).
struct SmallWordView: View {
    let model: WordWidgetModel
    @Environment(\.dynamicTypeSize) private var typeSize

    var body: some View {
        VStack(alignment: .leading, spacing: WidgetMetrics.s1) {
            if let category = model.category {
                CategoryChip(text: category, palette: model.palette)
            }
            Spacer(minLength: 0)
            EnglishText(model.word)
                .font(WidgetFont.word(22, relativeTo: .title2))
                .foregroundStyle(model.palette.word)
                .lineLimit(1)
                .minimumScaleFactor(0.7)
                .widgetAccentable()
            if let ipa = model.ipa, typeSize < .xLarge {
                Text(ipa)
                    .font(.system(size: 12))
                    .foregroundStyle(model.palette.ipa)
                    .lineLimit(1)
            }
            Text(model.translation)
                .font(WidgetFont.label(15, relativeTo: .subheadline))
                .foregroundStyle(model.palette.translation)
                .lineLimit(2)
                .minimumScaleFactor(0.8)
            Spacer(minLength: 0)
            HStack(alignment: .center, spacing: WidgetMetrics.s2) {
                if let progress = model.progressText {
                    Text(progress)
                        .font(.system(size: 12))
                        .foregroundStyle(model.palette.ipa)
                        .lineLimit(1)
                        .privacySensitive()
                }
                Spacer(minLength: 0)
                NextButton(palette: model.palette)
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .leading)
        .widgetURL(model.url(for: .systemSmall))
    }
}

/// 4×2: word and chip on one line, IPA · part of speech, translation, one line of example, actions row.
struct MediumWordView: View {
    let model: WordWidgetModel
    @Environment(\.dynamicTypeSize) private var typeSize

    var body: some View {
        VStack(alignment: .leading, spacing: WidgetMetrics.s1) {
            HStack(alignment: .firstTextBaseline, spacing: WidgetMetrics.s2) {
                Link(destination: model.url(for: .systemMedium)) {
                    EnglishText(model.word)
                        .font(WidgetFont.word(26, relativeTo: .title))
                        .foregroundStyle(model.palette.word)
                        .lineLimit(1)
                        .minimumScaleFactor(0.7)
                        .widgetAccentable()
                }
                Spacer(minLength: WidgetMetrics.s2)
                if let category = model.category {
                    CategoryChip(text: category, palette: model.palette)
                }
            }
            MetaLine(ipa: typeSize < .xLarge ? model.ipa : nil, pos: model.partOfSpeechShort, color: model.palette.ipa, ipaSize: 13, posSize: 12)
            Text(model.translation)
                .font(WidgetFont.label(17, relativeTo: .body))
                .foregroundStyle(model.palette.translation)
                .lineLimit(1)
                .minimumScaleFactor(0.8)
            if let example = model.example, typeSize < .xxLarge {
                Text("«\(example)»")
                    .font(.system(size: 13))
                    .foregroundStyle(model.palette.example)
                    .lineLimit(1)
            }
            Spacer(minLength: 0)
            HStack(alignment: .center, spacing: WidgetMetrics.s3) {
                QuizLink(model: model)
                Spacer(minLength: 0)
                ProgressBadge(model: model)
                NextButton(palette: model.palette)
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .leading)
    }
}

/// 4×4: «Слово години», the full card, the three recent words and the quiz pill.
struct LargeWordView: View {
    let model: WordWidgetModel
    @Environment(\.dynamicTypeSize) private var typeSize

    var body: some View {
        VStack(alignment: .leading, spacing: WidgetMetrics.s2) {
            HStack(alignment: .firstTextBaseline) {
                Text("widget.wordOfHour")
                    .font(WidgetFont.label(12, relativeTo: .caption))
                    .foregroundStyle(model.palette.ipa)
                Spacer(minLength: WidgetMetrics.s2)
                if let category = model.category {
                    CategoryChip(text: category, palette: model.palette)
                }
            }
            Link(destination: model.url(for: .systemLarge)) {
                VStack(alignment: .leading, spacing: WidgetMetrics.s1) {
                    EnglishText(model.word)
                        .font(WidgetFont.word(30, relativeTo: .largeTitle))
                        .foregroundStyle(model.palette.word)
                        .lineLimit(1)
                        .minimumScaleFactor(0.7)
                        .widgetAccentable()
                    MetaLine(ipa: typeSize < .xLarge ? model.ipa : nil, pos: model.partOfSpeechLong, color: model.palette.ipa, ipaSize: 15, posSize: 13)
                    Text(model.translation)
                        .font(WidgetFont.label(20, relativeTo: .title3))
                        .foregroundStyle(model.palette.translation)
                        .lineLimit(2)
                    if let example = model.example, typeSize < .xxLarge {
                        Text("«\(example)»")
                            .font(.system(size: 15))
                            .foregroundStyle(model.palette.example)
                            .lineLimit(3)
                    }
                }
                .frame(maxWidth: .infinity, alignment: .leading)
            }
            if !model.recent.isEmpty {
                Divider().overlay(model.palette.divider)
                Text("widget.recent")
                    .font(WidgetFont.label(12, relativeTo: .caption))
                    .foregroundStyle(model.palette.ipa)
                ForEach(model.recent) { recent in
                    Link(destination: DeepLinks.word(recent.lexemeId, source: DeepLinks.source(for: .systemLarge))) {
                        HStack(spacing: WidgetMetrics.s2) {
                            Circle().fill(model.palette.accent).frame(width: 5, height: 5)
                            EnglishText(recent.word)
                                .font(WidgetFont.label(15, relativeTo: .subheadline))
                                .foregroundStyle(model.palette.word)
                                .lineLimit(1)
                            Spacer(minLength: WidgetMetrics.s2)
                            Text(model.translationHidden ? "···" : recent.translation)
                                .font(.system(size: 15))
                                .foregroundStyle(model.palette.translation)
                                .lineLimit(1)
                        }
                    }
                }
            }
            Spacer(minLength: 0)
            Divider().overlay(model.palette.divider)
            HStack(alignment: .center, spacing: WidgetMetrics.s3) {
                Link(destination: model.quizURL) {
                    Text(model.quizLabel)
                        .font(WidgetFont.label(14, relativeTo: .subheadline))
                        .foregroundStyle(model.palette.onAccent)
                        .lineLimit(1)
                        .padding(.horizontal, WidgetMetrics.s3)
                        .padding(.vertical, WidgetMetrics.s2)
                        .background(Capsule().fill(model.palette.accent))
                }
                Spacer(minLength: 0)
                ProgressBadge(model: model)
                NextButton(palette: model.palette)
            }
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .leading)
    }
}

/// «/ˌsɛrənˈdɪpəti/ · ім.»; either part may be missing.
struct MetaLine: View {
    let ipa: String?
    let pos: String?
    let color: Color
    let ipaSize: CGFloat
    let posSize: CGFloat

    var body: some View {
        if ipa != nil || pos != nil {
            HStack(spacing: WidgetMetrics.s1) {
                if let ipa = ipa {
                    Text(ipa).font(.system(size: ipaSize))
                }
                if ipa != nil && pos != nil {
                    Text("·").font(.system(size: posSize))
                }
                if let pos = pos {
                    Text(pos).font(.system(size: posSize))
                }
            }
            .foregroundStyle(color)
            .lineLimit(1)
        }
    }
}
