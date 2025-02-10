using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

public class SegmentGroupConverter : JsonConverter<SegmentGroup>
{
    public override SegmentGroup ReadJson(JsonReader reader, Type objectType, SegmentGroup existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.String)
        {
            return new SegmentGroup { Segment = reader.Value.ToString() };
        }
        else if (reader.TokenType == JsonToken.StartObject)
        {
            JObject obj = JObject.Load(reader);
            return new SegmentGroup
            {
                Segment = obj["segment"]?.ToString(),
                Precondition = obj["precondition"]?.ToString(),
                GroupSegment = obj["segmentGroup"]?.ToString()
            };
        }
        throw new JsonSerializationException("Unexpected token type: " + reader.TokenType);
    }

    public override void WriteJson(JsonWriter writer, SegmentGroup value, JsonSerializer serializer)
    {
        writer.WriteStartObject();
        if (value.Segment != null)
        {
            writer.WritePropertyName("segment");
            writer.WriteValue(value.Segment);
        }
        if (value.Precondition != null)
        {
            writer.WritePropertyName("precondition");
            writer.WriteValue(value.Precondition);
        }
        if (value.GroupSegment != null)
        {
            writer.WritePropertyName("segmentGroup");
            writer.WriteValue(value.GroupSegment);
        }
        writer.WriteEndObject();
    }
}
