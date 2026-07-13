using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RhythmGame.Network.Api
{
    /// <summary>
    /// Go サーバーの譜面 JSON (snake_case フラット形式) をクライアントの <see cref="ChartData"/> に変換する。
    ///
    /// サーバー形式 (internal/chart/chart.go):
    ///   {"chart_id","song_id","difficulty","version","bpm","offset_ms",
    ///    "events":[{"type":"bpm"|"timesig"|"speed","time_ms",...}],
    ///    "notes":[{"time_ms","lane":0-5 または "0".."3"/"fxL"/"fxR","type":"tap"|"hold","duration_ms"}]}
    ///   lane 4="fxL" / 5="fxR"。FX レーン上の tap/hold はクライアントの FxTap/FxHold に対応する。
    ///   events の bpm は途中変化を含む (無ければトップレベル bpm にフォールバック=サーバーと同一)。
    ///
    /// TotalNotes(採点イベント総数) は <see cref="ScoringEventCounter"/> で再計算する
    /// (サーバー engine/count.go と同一意味論 — tap=1 / hold=head+ticks+tail)。
    /// ChartHash はサーバー API の chart_hash (譜面ファイル SHA-256) を正として設定する。
    /// </summary>
    public static class ServerChartConverter
    {
        /// <summary>サーバー譜面 JSON を ChartData に変換する。失敗時は ChartParseException。</summary>
        public static ChartData Convert(string serverJson, int level, string chartHash)
        {
            JObject root;
            try { root = JObject.Parse(serverJson); }
            catch (JsonException ex)
            {
                throw new ChartParseException("サーバー譜面 JSON の解析に失敗: " + ex.Message, ex);
            }

            string songId     = root.Value<string>("song_id") ?? "";
            string difficulty = root.Value<string>("difficulty") ?? "";
            int    version    = root.Value<int?>("version") ?? 1;
            double bpm        = root.Value<double?>("bpm") ?? 120.0;

            var notes = new List<NoteData>();
            int id = 1;
            var notesArr = root["notes"] as JArray;
            if (notesArr != null)
            {
                foreach (var n in notesArr)
                {
                    var (lane, isFx) = ParseLane(n["lane"]);
                    string type = n.Value<string>("type") ?? "tap";
                    bool isHold = type == "hold" || type == "fxHold";

                    notes.Add(new NoteData
                    {
                        Id         = id++,
                        Type       = isHold ? (isFx ? NoteType.FxHold : NoteType.Hold)
                                            : (isFx ? NoteType.FxTap  : NoteType.Tap),
                        Lane       = lane,
                        TimeMs     = n.Value<double?>("time_ms") ?? 0,
                        DurationMs = n.Value<double?>("duration_ms") ?? 0,
                    });
                }
            }

            // テンポ系イベントをサーバー JSON の events[] から取り込む (chart.go parseSeedChart と同一意味論)。
            //   bpm     — 途中変化を含めて全て採用。スコア(ホールドtick間隔)が BPM に追従するため、
            //             ここで落とすとサーバー engine/count.go とクライアントの判定・満点が乖離する。
            //   timesig — 変拍子。無ければ全編 4/4 (BpmTimeline 既定)。
            //   speed   — スクロール速度演出 (視覚専用・スコア不変)。
            // bpm イベントが 1 つも無い場合のみ、トップレベル bpm (≤0 は 120) を t=0 に合成する
            // (サーバーのフォールバックと同じ)。
            var events  = new List<TempoEvent>();
            bool hasBpm = false;
            var eventsArr = root["events"] as JArray;
            if (eventsArr != null)
            {
                foreach (var ev in eventsArr)
                {
                    switch (ev.Value<string>("type") ?? "")
                    {
                        case "bpm":
                            events.Add(new TempoEvent
                            {
                                Type       = "bpm",
                                TimeMs     = ev.Value<double?>("time_ms") ?? 0,
                                Bpm        = ev.Value<double?>("bpm") ?? 0,
                                Multiplier = 1.0,
                            });
                            hasBpm = true;
                            break;

                        case "timesig":
                            int num = ev.Value<int?>("numerator")   ?? 0;
                            int den = ev.Value<int?>("denominator") ?? 0;
                            if (num <= 0 || den <= 0) continue;
                            events.Add(new TempoEvent
                            {
                                Type        = "timesig",
                                TimeMs      = ev.Value<double?>("time_ms") ?? 0,
                                Numerator   = num,
                                Denominator = den,
                            });
                            break;

                        case "speed":
                            events.Add(new TempoEvent
                            {
                                Type       = "speed",
                                TimeMs     = ev.Value<double?>("time_ms") ?? 0,
                                Multiplier = ev.Value<double?>("multiplier") ?? 1.0,
                            });
                            break;
                    }
                }
            }
            if (!hasBpm)
            {
                if (bpm <= 0) bpm = 120.0;
                events.Insert(0, new TempoEvent { Type = "bpm", TimeMs = 0, Bpm = bpm, Multiplier = 1.0 });
            }

            var timeline = new BpmTimeline(events);
            int totalScoringEvents = ScoringEventCounter.Count(notes, timeline);
            if (totalScoringEvents == 0) totalScoringEvents = 1;

            return new ChartData
            {
                Version    = version,
                SongId     = songId,
                Difficulty = difficulty,
                Level      = level,
                Tags       = new List<string>(),
                ChartHash  = chartHash,
                TotalNotes = totalScoringEvents,
                Events     = events,
                Notes      = notes,
            };
        }

        // lane は int (0-5) と string ("0".."3"/"fxL"/"fxR") の両形式 (サーバーパーサーと同じ許容)
        static (LaneRef lane, bool isFx) ParseLane(JToken token)
        {
            if (token == null) throw new ChartParseException("lane フィールドがありません");

            if (token.Type == JTokenType.Integer)
            {
                int v = token.Value<int>();
                switch (v)
                {
                    case 0: return (LaneRef.Lane0, false);
                    case 1: return (LaneRef.Lane1, false);
                    case 2: return (LaneRef.Lane2, false);
                    case 3: return (LaneRef.Lane3, false);
                    case 4: return (LaneRef.FxL, true);
                    case 5: return (LaneRef.FxR, true);
                    default: throw new ChartParseException($"不明な lane 値: {v}");
                }
            }

            string s = token.Value<string>();
            switch (s)
            {
                case "0":   return (LaneRef.Lane0, false);
                case "1":   return (LaneRef.Lane1, false);
                case "2":   return (LaneRef.Lane2, false);
                case "3":   return (LaneRef.Lane3, false);
                case "fxL": return (LaneRef.FxL, true);
                case "fxR": return (LaneRef.FxR, true);
                default:    throw new ChartParseException($"不明な lane 値: '{s}'");
            }
        }
    }
}
