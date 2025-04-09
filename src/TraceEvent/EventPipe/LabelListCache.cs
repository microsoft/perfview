using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Diagnostics.Tracing.EventPipe
{


    enum LabelKind : byte
    {
        ActivityId = 1,
        RelatedActivityId = 2,
        TraceId = 3,
        SpanId = 4,
        KeyValueString = 5,
        KeyValueVarInt = 6
    }

    struct LabelList
    {
        internal static LabelList Parse(ref SpanReader reader)
        {
            byte kindAndFlag = 0;
            LabelList result = new LabelList();
            List<KeyValuePair<string, object>> otherLabels = null;
            do
            {
                kindAndFlag = reader.ReadUInt8();
                LabelKind kind = (LabelKind)(kindAndFlag & 0x7F);
                switch(kind)
                {
                    case LabelKind.ActivityId:
                        result.ActivityId = reader.Read<Guid>();
                        break;
                    case LabelKind.RelatedActivityId:
                        result.RelatedActivityId = reader.Read<Guid>();
                        break;
                    case LabelKind.TraceId:
                        result.TraceId = reader.Read<Guid>();
                        break;
                    case LabelKind.SpanId:
                        result.SpanId = reader.Read<ulong>();
                        break;
                    case LabelKind.KeyValueString:
                        string key = reader.ReadVarUIntUTF8String();
                        string value = reader.ReadVarUIntUTF8String();
                        if (otherLabels == null)
                        {
                            otherLabels = new List<KeyValuePair<string, object>>();
                        }
                        otherLabels.Add(new KeyValuePair<string, object>(key, value));
                        break;
                    case LabelKind.KeyValueVarInt:
                        key = reader.ReadVarUIntUTF8String();
                        long varInt = reader.ReadVarInt64();
                        if (otherLabels == null)
                        {
                            otherLabels = new List<KeyValuePair<string, object>>();
                        }
                        otherLabels.Add(new KeyValuePair<string, object>(key, varInt));
                        break;
                    default:
                        throw new FormatException($"Unknown label kind {kind} in label list");
                }

            } while ((kindAndFlag & 0x80) == 0);
            if(otherLabels != null)
            {
                result.OtherLabels = otherLabels.ToArray();
            }
            return result;
        }

        public Guid? ActivityId { get; private set; }
        public Guid? RelatedActivityId { get; private set; }
        public Guid? TraceId { get; private set; }
        public ulong? SpanId { get; private set; }
        public KeyValuePair<string, object>[] OtherLabels { get; private set; }

        public IEnumerable<KeyValuePair<string, object>> AllLabels
        {
            get
            {
                if (ActivityId.HasValue)
                {
                    yield return new KeyValuePair<string, object>("ActivityId", ActivityId.Value.ToString());
                }
                if (RelatedActivityId.HasValue)
                {
                    yield return new KeyValuePair<string, object>("RelatedActivityId", RelatedActivityId.Value.ToString());
                }
                if (TraceId.HasValue)
                {
                    yield return new KeyValuePair<string, object>("TraceId", TraceId.Value.ToString());
                }
                if (SpanId.HasValue)
                {
                    yield return new KeyValuePair<string, object>("SpanId", (long)SpanId.Value);
                }
                if (OtherLabels != null)
                {
                    foreach (var label in OtherLabels)
                    {
                        yield return label;
                    }
                }
            }
        }
    }

    internal class LabelListCache
    {
        Dictionary<int, LabelList> _labelLists = new Dictionary<int, LabelList>();

        public void ProcessLabelListBlock(Block block)
        {
            SpanReader reader = block.Reader;

            int curIndex = reader.ReadInt32();
            int count = reader.ReadInt32();
            for(int i = 0; i < count; i++, curIndex++)
            {
                LabelList labelList = LabelList.Parse(ref reader);
                _labelLists[curIndex] = labelList;
            }
        }

        public void Flush()
        {
            _labelLists.Clear();
        }

        public LabelList GetLabelList(int index)
        {
            if(index == 0)
            {
                return new LabelList();
            }
            if (_labelLists.TryGetValue(index, out LabelList labelList))
            {
                return labelList;
            }
            else
            {
                throw new FormatException($"Reference to unknown label list index {index}");
            }
        }
    }
}
