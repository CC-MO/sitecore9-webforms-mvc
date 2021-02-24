using Sitecore.Pipelines;
using System.Web.Mvc;
using System.Web.Routing;

namespace CCIS.SC.Feature.WebFormsMvc.Processors
{
	public class RegisterWebFormsRoutes
	{
		public void Process(PipelineArgs args)
		{
			RouteTable.Routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
		}
	}
}
