using Sitecore.Pipelines.HttpRequest;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;

namespace CCIS.SC.Feature.WebFormsMvc.Processors
{
	public class CleanWebFormsAjaxOutput : HttpRequestProcessor
	{
		internal Regex CleanOutput = new Regex(
			"<ccis-ajax>((?:.|\r|\n)+?)</ccis-ajax>",
			RegexOptions.Compiled | RegexOptions.Multiline);

		public override void Process(HttpRequestArgs args)
		{
#pragma warning disable CS0618 // Type or member is obsolete
			if (!(bool)args.Context.Request.Form?.AllKeys.Contains("__ASYNCPOST"))
				return;

			var res = args.Context.Response;
			var filter = new ResponseFilterStream(res.Filter);

			filter.TransformString += (string output) =>
			{
				if (!HttpContext.Current.Items.Contains("CCIS.Ajax"))
					return output;

				var matches = CleanOutput.Match(output);

				if (matches.Groups.Count > 1)
					return matches.Groups[1].Value;

				return output;
			};

			args.Context.Response.Filter = filter;
#pragma warning restore CS0618 // Type or member is obsolete
		}
	}
}
