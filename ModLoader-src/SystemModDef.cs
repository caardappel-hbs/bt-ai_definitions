using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using BattleTech.ModSupport;
using HBS.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class SystemModDef : BaseModDef
{
    public override string ToJSON()
    {
        return JSONSerializationUtility.ToJSON(this);
    }

    public override void FromJSON(string json)
    {
        JsonConvert.PopulateObject(json,this);
    }

    public override string GenerateJSONTemplate()
    {
        return JSONSerializationUtility.ToJSON(new SystemModDef());
    }
}
