// http://www.chrisvandesteeg.nl/2014/02/11/usercontrol-renderings-in-a-sitecore-mvc-website-wffm-for-mvc/

using Sitecore.Configuration;
using Sitecore.Data.Items;
using Sitecore.Data.Templates;
using Sitecore.Mvc.Common;
using Sitecore.Mvc.Presentation;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Mvc;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using Rendering = Sitecore.Mvc.Presentation.Rendering;

namespace CCIS.SC.Feature.WebFormsMvc.Renderers
{
	public class UserControlRenderer : Renderer
	{
		public override void Render(TextWriter writer)
		{
			var current = ContextService.Get().GetCurrent<ViewContext>();
			var currentWriter = current.Writer;

			try
			{
				current.Writer = writer;
				// in itemvisualization.getrenderings, the context is swithed to shell#lang cookie???
				// so if you're  logged in into sitecore cms, you'll get the renderings in an incorrect language!
				HttpContext.Current.Request.Cookies.Remove("shell#lang");
				new SitecorePlaceholder(Rendering.RenderingItem).RenderView(current, Rendering);
			}
			finally
			{
				current.Writer = currentWriter;
			}
		}

		public Rendering Rendering { get; set; }

		public Template RenderingTemplate { get; set; }
	}

	internal class SitecorePlaceholder : ViewUserControl
	{
		public SitecorePlaceholder(RenderingItem item)
		{
			Item = item;
		}

		public RenderingItem Item { get; }

		protected override void OnInit(EventArgs e)
		{
			base.OnInit(e);

			var form = new SitecoreForm();

			Controls.Add(form);
			System.Diagnostics.Debug.WriteLine("Pagerenderings: " + Sitecore.Context.Page.Renderings.Count);

			var subLayout = Sitecore.Context.Page.Renderings.First(rrf => rrf.RenderingID == Item.ID).GetControl();

			form.Controls.Add(subLayout);
		}

		public override void RenderView(ViewContext viewContext)
		{
			RenderView(viewContext, null);
		}

		public void RenderView(ViewContext viewContext, Rendering rendering)
		{
			var prevHandler = Context.Handler;

			using (var containerPage = new PageHolderContainerPage(this))
			{
				try
				{
					Context.Handler = containerPage;

					if (Sitecore.Context.Page == null)
					{
						viewContext.Writer.WriteLine("<!-- Unable to use sitecoreplacholder outside sitecore -->");

						return;
					}

					InitializePageContext(containerPage, viewContext);
					RenderViewAndRestoreContentType(containerPage, viewContext);
				}
				finally
				{
					Context.Handler = prevHandler;
				}
			}
		}

		internal static MethodInfo pageContextInitializer = typeof(Sitecore.Layouts.PageContext).GetMethod("Initialize", BindingFlags.NonPublic | BindingFlags.Instance);

		internal static MethodInfo pageContextOnPreRender = typeof(Sitecore.Layouts.PageContext).GetMethod("OnPreRender", BindingFlags.NonPublic | BindingFlags.Instance);

		internal static FieldInfo pageContext_page = typeof(Sitecore.Layouts.PageContext).GetField("page", BindingFlags.NonPublic | BindingFlags.Instance);

		internal static void InitializePageContext(Page containerPage, ViewContext viewContext)
		{
			var pageContext = Sitecore.Context.Page;

			if (pageContext == null)
				return;

			pageContext_page.SetValue(pageContext, containerPage);

			var exists = pageContext.Renderings != null && pageContext.Renderings.Count > 0;

			if (!exists)
			{
				// use the default initializer:
				pageContextInitializer.Invoke(pageContext, null);
			}
			else
			{
				// our own initializer (almost same as Initialize in PageContext, but we need to skip buildcontroltree, since that is already availabe)
				containerPage.PreRender += (sender, args) => pageContextOnPreRender.Invoke(pageContext, new[] { sender, args });

				switch (Settings.LayoutPageEvent)
				{
					case "preInit":
						containerPage.PreInit += (o, args) => pageContext.Build();
						break;

					case "init":
						containerPage.Init += (o, args) => pageContext.Build();
						break;

					case "load":
						containerPage.Load += (o, args) => pageContext.Build();
						break;
				}
			}
		}

		internal static void RenderViewAndRestoreContentType(ViewPage containerPage, ViewContext viewContext)
		{
			// We need to restore the Content-Type since Page.SetIntrinsics() will reset it. It's not possible
			// to work around the call to SetIntrinsics() since the control's render method requires the
			// containing page's Response property to be non-null, and SetIntrinsics() is the only way to set
			string savedContentType = viewContext.HttpContext.Response.ContentType;

			containerPage.RenderView(viewContext);
			viewContext.HttpContext.Response.ContentType = savedContentType;
		}

		internal sealed class PageHolderContainerPage : ViewPage
		{
			private readonly ViewUserControl _userControl;

			public PageHolderContainerPage(ViewUserControl userControl)
			{
				_userControl = userControl;
			}

			public override void ProcessRequest(HttpContext context)
			{
				_userControl.ID = "CCIS_Legacy_UserControl_" + NextId();

				// CCIS custom addition to handle UpdatePanel AJAX responses
				var ajaxResponse = false;

				if (context.Request.Form?["__ASYNCPOST"] == "true"
					&& context.Request.Form.AllKeys.Where(k => k.StartsWith($"{_userControl.ID}$")).Any())
				{
					context.Items["CCIS.Ajax"] = true;
					ajaxResponse = true;
					context.Response.Write("<ccis-ajax>");
				}
				// end customization

				Controls.Add(_userControl);
				base.ProcessRequest(context);

				// CCIS custom addition to handle UpdatePanel AJAX responses
				if (ajaxResponse)
					context.Response.Write("</ccis-ajax>");
				// end customization
			}

			internal string NextId()
			{
				var currentId = Context.Items.Contains("PageHolderContainerPage.nextId") ? (int)Context.Items["PageHolderContainerPage.nextId"] : 1;

				return (Context.Items["PageHolderContainerPage.nextId"] = (currentId + 1)).ToString();
			}
		}
	}

	internal class SitecoreForm : HtmlForm
	{
		protected override void AddedControl(Control control, int index)
		{
			base.AddedControl(control, index);

			var reference = Sitecore.Context.Page.GetRenderingReference(control);

			if (reference != null)
				reference.AddedToPage = true;
		}

		protected override void Render(HtmlTextWriter output)
		{
			if (Controls.Count > 0)
				base.Render(output);
		}
	}
}