using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System.ComponentModel.DataAnnotations;
/// <summary>
/// GetEventResponse
/// </summary>
[DataContract(Name = "GetEventResponse")]
public partial class ClientEventDetailsResponse : IValidatableObject
{

    /// <summary>
    /// Gets or Sets Event
    /// </summary>
    [DataMember(Name = "event", IsRequired = true, EmitDefaultValue = true)]
    public ClientEventData Event { get; set; }

    /// <summary>
    /// Data for the markets in this event. This field is deprecated in favour of the \&quot;markets\&quot; field inside the event. Which will be filled with the same value if you use the query parameter \&quot;with_nested_markets&#x3D;true\&quot;.
    /// </summary>
    /// <value>Data for the markets in this event. This field is deprecated in favour of the \&quot;markets\&quot; field inside the event. Which will be filled with the same value if you use the query parameter \&quot;with_nested_markets&#x3D;true\&quot;.</value>
    [DataMember(Name = "markets", IsRequired = true, EmitDefaultValue = true)]
    public List<Market> Markets { get; set; }

    /// <summary>
    /// Returns the string presentation of the object
    /// </summary>
    /// <returns>String presentation of the object</returns>
    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("class GetEventResponse {\n");
        sb.Append("  Event: ").Append(Event).Append("\n");
        sb.Append("  Markets: ").Append(Markets).Append("\n");
        sb.Append("}\n");
        return sb.ToString();
    }

    /// <summary>
    /// Returns the JSON string presentation of the object
    /// </summary>
    /// <returns>JSON string presentation of the object</returns>
    public virtual string ToJson()
    {
        return Newtonsoft.Json.JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented);
    }

    /// <summary>
    /// To validate all properties of the instance
    /// </summary>
    /// <param name="validationContext">Validation context</param>
    /// <returns>Validation Result</returns>
    IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
    {
        yield break;
    }
}