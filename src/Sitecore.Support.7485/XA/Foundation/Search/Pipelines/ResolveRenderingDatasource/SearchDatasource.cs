namespace Sitecore.Support.XA.Foundation.Search.Pipelines.ResolveRenderingDatasource
{

  using Sitecore.ContentSearch;
  using Sitecore.ContentSearch.Utilities;
  using Sitecore.Data.Items;
  using Sitecore.Diagnostics;
  using Sitecore.Pipelines.ResolveRenderingDatasource;
  using Sitecore.Text;
  using Sitecore.XA.Foundation.IoC;
  using Sitecore.XA.Foundation.Multisite;
  using Sitecore.XA.Foundation.Search.Models;
  using Sitecore.XA.Foundation.Search.Services;
  using Sitecore.XA.Foundation.SitecoreExtensions.Extensions;
  using System.Collections.Generic;
  using System.Linq;

  public class SearchDatasource : Sitecore.XA.Foundation.Search.Pipelines.ResolveRenderingDatasource.SearchDatasource
  {    

    public SearchDatasource()
    {
      MultisiteContext = ServiceLocator.Current.Resolve<IMultisiteContext>();
    }

    public void Process(ResolveRenderingDatasourceArgs args)
    {
      Assert.IsNotNull(args, "args");
      if (args.Datasource.Length > 1 && args.Datasource.Contains(":"))
      {
        Item contextItem = args.GetContextItem();
        if (contextItem != null)
        {
          List<SearchStringModel> model = new List<SearchStringModel>();
          model.AddRange(SearchStringModel.ParseDatasourceString(args.Datasource));
          model.AddRange(GetPageScope(contextItem));

          IProviderSearchContext searchContext = GetSearchContext(contextItem);
          Item startLocationItem = contextItem.Database.GetItem(ItemIDs.RootID);
          IQueryable<ExtendedSearchResultItem> query = LinqHelper.CreateQuery<ExtendedSearchResultItem>(searchContext, model.RemoveWhere(m => m.Type == "sort"), startLocationItem);

          Item siteItem = MultisiteContext.GetSiteItem(contextItem);
          if (siteItem == null)
          {
            args.Datasource = string.Empty;
            return;
          }

          Item settingsItem = MultisiteContext.GetSettingsItem(siteItem);
          var datasourceSearchScopeIds = GetDatasourceSearchScopesIds(settingsItem);
          if (datasourceSearchScopeIds.Count == 0)
          {
            datasourceSearchScopeIds.Add(siteItem.ID.ToSearchID());
          }

          query = query.Where(BuildPathPredicate(datasourceSearchScopeIds));
          query = query.Where(i => i.Language == Context.Language.Name);

          foreach (SearchStringModel sort in model.Where(m => m.Type == "sort"))
          {
            string key = sort.Value.EndsWith("[desc]")
                                  ? sort.Value.Substring(0, sort.Value.Length - "[desc]".Length).Trim()
                                  : sort.Value.Trim();
            Item facetItem = FacetService.GetFacetItems(new[] { key }).FirstOrDefault();
            bool floatFacet = false, integerFacet = false;
            if (facetItem != null)
            {
              floatFacet = facetItem.DoesItemInheritFrom(Sitecore.XA.Foundation.Search.Templates.FloatFacet.ID);
              integerFacet = facetItem.DoesItemInheritFrom(Sitecore.XA.Foundation.Search.Templates.IntegerFacet.ID);
            }

            if (sort.Value.EndsWith("[desc]"))
            {
              if (floatFacet)
              {
                query = query.OrderByDescending(i => i.get_Item<double>(key));
              }
              else if (integerFacet)
              {
                query = query.OrderByDescending(i => i.get_Item<long>(key));
              }
              else
              {
                query = query.OrderByDescending(i => i[key]);
              }
            }
            else
            {
              if (floatFacet)
              {
                query = query.OrderBy(i => i.get_Item<double>(key));
              }
              else if (integerFacet)
              {
                query = query.OrderBy(i => i.get_Item<long>(key));
              }
              else
              {
                query = query.OrderBy(i => i[key]);
              }
            }
          }
          query = query.Take(QueryMaxItems);

          ListString itemIds = new ListString(query.Select(r => r.ItemId.ToString()).ToList());
          args.Datasource = itemIds.ToString();
          return;
        }

        args.Datasource = string.Empty;
      }
    }

  }
}