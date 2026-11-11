var paths=new System.Collections.Generic.List<string>();
foreach(var t in new[]{77.8f,89.8f,101.9f,97.1f,116.7f})paths.Add(OpeningRemakeV2.Preview(t,"polish-"+t.ToString("F1",System.Globalization.CultureInfo.InvariantCulture),1280,720));
return string.Join(" | ",paths.ToArray());
