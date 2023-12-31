﻿using eCommerce.Core.Interfaces;
using System.Linq.Expressions;
using System.Reflection;
using eCommerce.Core.Primitives;
using eCommerce.Core.Entities.Configuration.CustomSettings;
using eCommerce.Core.Entities.Localization;
using eCommerce.Core.Shared;
using eCommerce.Core.Services.Caching;
using eCommerce.Core.Services.Configuration;
using eCommerce.Core.Services.Localization;

namespace eCommerce.Infrastructure.Services.Localization;

public class LocalizationService : ILocalizationService
{
    #region Fields

    private readonly LocalizationSettings _localizationSettings;

    private readonly IStaticCacheManager _staticCacheManager;

    private readonly ILanguageService _languageService;

    private readonly ILocalizedEntityService _localizedEntityService;

    private readonly ISettingService _settingService;

    private readonly IWorkContext _workContext;

    private readonly IRepository<LocaleStringResource> _localizationRepsitory;

    #endregion

    #region Constructure and Destructure

    public LocalizationService(
        ILanguageService languageService,
        IStaticCacheManager staticCacheManager,
        IWorkContext workContext,
        ILocalizedEntityService localizedEntityService,
        ISettingService settingService,
        LocalizationSettings localizationSettings,
        IRepository<LocaleStringResource> localizationRepsitory)
    {
        _languageService = languageService;
        _staticCacheManager = staticCacheManager;
        _workContext = workContext;
        _localizedEntityService = localizedEntityService;
        _settingService = settingService;
        _localizationSettings = localizationSettings;
        _localizationRepsitory = localizationRepsitory;
    }

    #endregion

    #region Public Methods

    public void AddOrUpdateLocaleResource(IDictionary<string, string> resources, Guid? languageId = null)
    {
        var resourcesToInsert = UpdateLocaleResource(resources, languageId, false);

        if (resourcesToInsert.Any())
        {
            var locales = _languageService.GetAllLanguages(true)
                .Where(language => !languageId.HasValue || language.Id == languageId.Value)
                .SelectMany(language => resourcesToInsert.Select(resource => new LocaleStringResource
                {
                    LanguageId = language.Id,
                    ResourceName = resource.Key.Trim().ToLowerInvariant(),
                    ResourceValue = resource.Value
                }))
                .ToList();

            _localizationRepsitory.Insert(locales);
        }

        _staticCacheManager.RemoveByPrefix(EntityCacheDefaults<LocaleStringResource>.Prefix);
    }

    public async Task AddOrUpdateLocaleResourceAsync(string resourceName, string resourceValue, string? languageCulture = null)
    {
        foreach (var lang in await _languageService.GetAllLanguagesAsync(true))
        {
            if (!string.IsNullOrEmpty(languageCulture) && !languageCulture.Equals(lang.LanguageCulture))
            {
                continue;
            }

            var lsr = await GetLocaleStringResourceByNameAsync(resourceName, lang.Id, false);
            if (lsr is null)
            {
                lsr = new LocaleStringResource
                {
                    LanguageId = lang.Id,
                    ResourceName = resourceName,
                    ResourceValue = resourceValue
                };
                await InsertLocaleStringResourceAsync(lsr);
            }
            else
            {
                lsr.ResourceValue = resourceValue;
                await UpdateLocaleStringResourceAsync(lsr);
            }
        }
    }

    public async Task AddOrUpdateLocaleResourceAsync(IDictionary<string, string> resources, Guid? languageId = null)
    {
        var resourcesToInsert = await UpdateLocaleResourceAsync(resources, languageId, false);

        if (resourcesToInsert.Any())
        {
            var locales = (await _languageService.GetAllLanguagesAsync(true))
                .Where(language => !languageId.HasValue || language.Id == languageId.Value)
                .SelectMany(language => resourcesToInsert.Select(resource => new LocaleStringResource
                {
                    LanguageId = language.Id,
                    ResourceName = resource.Key.Trim().ToLowerInvariant(),
                    ResourceValue = resource.Value
                }))
                .ToList();

            await _localizationRepsitory.InsertAsync(locales);
        }

        await _staticCacheManager.RemoveByPrefixAsync(EntityCacheDefaults<LocaleStringResource>.Prefix);
    }

    public async Task DeleteLocaleResourceAsync(string resourceName)
    {
        foreach (var lang in await _languageService.GetAllLanguagesAsync(true))
        {
            var lsr = await GetLocaleStringResourceByNameAsync(resourceName, lang.Id, false);
            if (lsr is not null)
            {
                await DeleteLocaleStringResourceAsync(lsr);
            }
        }
    }

    public void DeleteLocaleResources(IList<string> resourceNames, Guid? languageId = null)
    {
        var resources = _localizationRepsitory.GetAll(query =>
        {
            query = query.Where(q =>
                (!languageId.HasValue || q.LanguageId == languageId) &&
                resourceNames.Contains(q.ResourceName, StringComparer.InvariantCultureIgnoreCase));

            return query;
        });

        if (resources is null || !resources.Any())
        {
            return;
        }

        foreach (var resource in resources)
        {
           _localizationRepsitory.Delete(resource);
        }

        _staticCacheManager.RemoveByPrefix(EntityCacheDefaults<LocaleStringResource>.Prefix);
    }

    public async Task DeleteLocaleResourcesAsync(IList<string> resourceNames, Guid? languageId = null)
    {
        var resources = await _localizationRepsitory.GetAllAsync(query =>
        {
            query = query.Where(q =>
                (!languageId.HasValue || q.LanguageId == languageId) &&
                resourceNames.Contains(q.ResourceName, StringComparer.InvariantCultureIgnoreCase));

            return query;
        });

        if (resources is null || !resources.Any())
        {
            return;
        }

        foreach (var resource in resources)
        {
            await _localizationRepsitory.DeleteAsync(resource);
        }

        await _staticCacheManager.RemoveByPrefixAsync(EntityCacheDefaults<LocaleStringResource>.Prefix);
    }

    public async Task DeleteLocaleResourcesAsync(string resourceNamePrefix, Guid? languageId = null)
    {
        var resources = await _localizationRepsitory.GetAllAsync(query =>
        {
            query = query.Where(q =>
                (!languageId.HasValue || q.LanguageId == languageId) &&
                !string.IsNullOrEmpty(q.ResourceName) &&
                q.ResourceName.StartsWith(resourceNamePrefix, StringComparison.InvariantCultureIgnoreCase));

            return query;
        });

        if (resources is null || !resources.Any())
        {
            return;
        }

        foreach (var resource in resources)
        {
            await _localizationRepsitory.DeleteAsync(resource);
        }

        await _staticCacheManager.RemoveByPrefixAsync(EntityCacheDefaults<LocaleStringResource>.Prefix);
    }

    public async Task DeleteLocaleStringResourceAsync(LocaleStringResource localeStringResource) => await _localizationRepsitory.DeleteAsync(localeStringResource);

    public async Task<Dictionary<string, KeyValuePair<Guid, string>>> GetAllResourceValuesAsync(Guid languageId, bool? loadPublicLocales)
    {
        var key = _staticCacheManager.PrepareKeyForDefaultCache(LocalizationDefaults.LocaleStringResourcesAllCacheKey, languageId);
        var allLocales = await _staticCacheManager.GetAsync<Dictionary<string, KeyValuePair<Guid, string>>>(key);

        if (!loadPublicLocales.HasValue || allLocales is not null)
        {
            var rez = allLocales ?? await _staticCacheManager.GetAsync(key, () =>
            {
                var resources = _localizationRepsitory.GetAll(query =>
                {
                    query = query
                        .Where(q => q.LanguageId == languageId)
                        .OrderBy(q => q.ResourceName);

                    return query;
                });

                return ResourceValuesToDictionary(resources);
            });

            await _staticCacheManager.RemoveAsync(LocalizationDefaults.LocaleStringResourcesAllPublicCacheKey, languageId);
            await _staticCacheManager.RemoveAsync(LocalizationDefaults.LocaleStringResourcesAllAdminCacheKey, languageId);

            return rez;
        }

        key = _staticCacheManager.PrepareKeyForDefaultCache(loadPublicLocales.Value
            ? LocalizationDefaults.LocaleStringResourcesAllPublicCacheKey
            : LocalizationDefaults.LocaleStringResourcesAllAdminCacheKey,
            languageId);

        return await _staticCacheManager.GetAsync(key, async () =>
        {
            var resources = await _localizationRepsitory.GetAllAsync(query =>
            {
                query = query.Where(q => q.LanguageId == languageId);

                if (loadPublicLocales.Value)
                {
                    query = query.Where(r => !r.ResourceName.StartsWith(LocalizationDefaults.AdminLocaleStringResourcesPrefix));
                }
                else
                {
                    query = query.Where(r => r.ResourceName.StartsWith(LocalizationDefaults.AdminLocaleStringResourcesPrefix));
                }

                query = query.OrderBy(q => q.ResourceName);

                return query;
            });

            return ResourceValuesToDictionary(resources);
        });
    }

    public async Task<LocaleStringResource> GetLocaleStringResourceByIdAsync(Guid localeStringResourceId) => await _localizationRepsitory.GetByIdAsync(localeStringResourceId);

    public LocaleStringResource? GetLocaleStringResourceByName(string resourceName, Guid languageId, bool logIfNotFound = true)
    {
        var localeStringResource = _localizationRepsitory.GetFirstOrDefault(
            func: q => q.Where(p => p.LanguageId == languageId && p.ResourceName == resourceName.ToLowerInvariant()));

        if (localeStringResource is null && logIfNotFound)
        {
            return null;
        }

        return localeStringResource;
    }

    public async Task<LocaleStringResource?> GetLocaleStringResourceByNameAsync(string resourceName, Guid languageId, bool logIfNotFound = true)
    {
        var localeStringResource = await _localizationRepsitory.GetFirstOrDefaultAsync(
            func: q => q.Where(p => p.LanguageId == languageId && p.ResourceName == resourceName.ToLowerInvariant()));

        if (localeStringResource == null && logIfNotFound)
        {
            return null;
        }

        return localeStringResource;
    }

    public async Task<TPropType> GetLocalizedAsync<TEntity, TPropType>(
        TEntity entity,
        Expression<Func<TEntity, TPropType>> keySelector,
        Guid? languageId = null,
        bool returnDefaultValue = true,
        bool ensureTwoPublishedLanguages = true) where TEntity : BaseEntity, ILocalizedEntity
    {
        if (entity is null)
        {
            throw new ArgumentNullException(nameof(entity));
        }

        if (keySelector.Body is not MemberExpression member)
        {
            throw new ArgumentException($"Expression '{keySelector}' refers to a method, not a property.");
        }

        if (member.Member is not PropertyInfo propInfo)
        {
            throw new ArgumentException($"Expression '{keySelector}' refers to a field, not a property.");
        }

        var result = default(TPropType);
        var resultStr = string.Empty;

        var localeKeyGroup = entity.GetType().Name;
        var localeKey = propInfo.Name;

        if (!languageId.HasValue)
        {
            var workingLanguage = await _workContext.GetWorkingLanguageAsync();
            languageId = workingLanguage.Id;
        }

        var loadLocalizedValue = true;
        if (ensureTwoPublishedLanguages)
        {
            var totalPublishedLanguages = (await _localizationRepsitory.GetAllAsync()).Count;
            loadLocalizedValue = totalPublishedLanguages >= 2;
        }

        if (loadLocalizedValue)
        {
            resultStr = await _localizedEntityService.GetLocalizedValueAsync(languageId.Value, entity.Id, localeKeyGroup, localeKey);
            if (!string.IsNullOrEmpty(resultStr))
            {
                result = CommonHelper.To<TPropType>(resultStr);
            }
        }

        if (!string.IsNullOrEmpty(resultStr) || !returnDefaultValue)
        {
            return result;
        }

        var localizer = keySelector.Compile();
        result = localizer(entity);

        return result;
    }

    public async Task<string> GetLocalizedEnumAsync<TEnum>(TEnum enumValue, Guid? languageId = null) where TEnum : struct
    {
        if (!typeof(TEnum).IsEnum)
        {
            throw new ArgumentException("T must be an enumerated type");
        }

        var workingLanguage = await _workContext.GetWorkingLanguageAsync();
        var resourceName = $"{LocalizationDefaults.EnumLocaleStringResourcesPrefix}{typeof(TEnum)}.{enumValue}";
        var result = await GetResourceAsync(resourceName, languageId ?? workingLanguage.Id, false, string.Empty, true);

        if (string.IsNullOrEmpty(result))
        {
            result = CommonHelper.ConvertEnum(enumValue.ToString());
        }

        return result;
    }

    public virtual async Task<string> GetLocalizedSettingAsync<TSettings>(TSettings settings, Expression<Func<TSettings, string>> keySelector,
            Guid languageId, bool returnDefaultValue = true, bool ensureTwoPublishedLanguages = true)
            where TSettings : ISettings, new()
    {
        var key = _settingService.GetSettingKey(settings, keySelector);
        var setting = await _settingService.GetSettingAsync(key, loadSharedValueIfNotFound: true);

        if (setting == null)
        {
            return null;
        }

        return await GetLocalizedAsync(setting, x => x.Value, languageId, returnDefaultValue, ensureTwoPublishedLanguages);
    }

    public async Task<string> GetResourceAsync(string resourceKey)
    {
        var workingLanguage = await _workContext.GetWorkingLanguageAsync();

        if (workingLanguage is not null)
        {
            return await GetResourceAsync(resourceKey, workingLanguage.Id);
        }

        return string.Empty;
    }

    public async Task<string> GetResourceAsync(
        string resourceKey,
        Guid languageId,
        bool logIfNotFound = true,
        string defaultValue = "",
        bool returnEmptyIfNotFound = false)
    {
        var result = string.Empty;

        resourceKey ??= string.Empty;
        resourceKey = resourceKey.Trim().ToLowerInvariant();

        if (_localizationSettings.LoadAllLocaleRecordsOnStartup)
        {
            var resources = await GetAllResourceValuesAsync(languageId, !resourceKey.StartsWith(LocalizationDefaults.AdminLocaleStringResourcesPrefix, StringComparison.InvariantCultureIgnoreCase));
            if (resources.ContainsKey(resourceKey))
            {
                result = resources[resourceKey].Value;
            }
        }
        else
        {
            var lsr = await _localizationRepsitory.GetFirstOrDefaultAsync(
                func: q => q.Where(p => p.ResourceName == resourceKey && p.LanguageId == languageId),
                getCacheKey: q => q.PrepareKeyForDefaultCache(LocalizationDefaults.LocaleStringResourcesByNameCacheKey, languageId, resourceKey));

            if (lsr is not null)
            {
                result = lsr.ResourceValue;
            }
        }

        if (!string.IsNullOrEmpty(result))
        {
            return result;
        }

        if (logIfNotFound)
        {
            // TODO
        }

        if (!string.IsNullOrEmpty(defaultValue))
        {
            result = defaultValue;
        }
        else
        {
            if (!returnEmptyIfNotFound)
                result = resourceKey;
        }

        return result;
    }

    public async Task InsertLocaleStringResourceAsync(LocaleStringResource localeStringResource)
    {
        if (!string.IsNullOrEmpty(localeStringResource?.ResourceName))
        {
            localeStringResource.ResourceName = localeStringResource.ResourceName.Trim().ToLowerInvariant();
        }

        await _localizationRepsitory.InsertAsync(localeStringResource);
    }

    public async Task SaveLocalizedSettingAsync<TSettings>(
        TSettings settings,
        Expression<Func<TSettings, string>> keySelector,
        Guid languageId,
        string value) where TSettings : ISettings, new()
    {
        var key = _settingService.GetSettingKey(settings, keySelector);
        var setting = await _settingService.GetSettingAsync(key);

        if (setting == null)
        {
            return;
        }

        await _localizedEntityService.SaveLocalizedValueAsync(setting, x => x.Value, value, languageId);
    }

    public void UpdateLocaleStringResource(LocaleStringResource localeStringResource) => _localizationRepsitory.Update(localeStringResource);

    public async Task UpdateLocaleStringResourceAsync(LocaleStringResource localeStringResource) => await _localizationRepsitory.UpdateAsync(localeStringResource);

    #endregion

    #region Methods

    private IDictionary<string, string> UpdateLocaleResource(IDictionary<string, string> resources, Guid? languageId = null, bool clearCache = true)
    {
        var localResources = new Dictionary<string, string>(resources, StringComparer.InvariantCultureIgnoreCase);
        var keys = localResources.Keys.Select(key => key.ToLowerInvariant()).ToArray();
        var resourcesToUpdate = _localizationRepsitory.GetAll(q => q.Where(p => p.LanguageId == languageId && keys.Contains(p.ResourceName.ToLower())));
        var existsResources = new List<string>();

        foreach (var localeStringResource in resourcesToUpdate.ToList())
        {
            var newValue = localResources[localeStringResource.ResourceName];

            if (localeStringResource.ResourceValue.Equals(newValue))
                resourcesToUpdate.Remove(localeStringResource);

            localeStringResource.ResourceValue = newValue;
            existsResources.Add(localeStringResource.ResourceName);
        }

        _localizationRepsitory.Update(resourcesToUpdate);

        if (clearCache)
        {
            _staticCacheManager.RemoveByPrefix(EntityCacheDefaults<LocaleStringResource>.Prefix);
        }

        return localResources
            .Where(item => !existsResources.Contains(item.Key, StringComparer.InvariantCultureIgnoreCase))
            .ToDictionary(p => p.Key, p => p.Value);
    }

    private async Task<IDictionary<string, string>> UpdateLocaleResourceAsync(
        IDictionary<string, string> resources,
        Guid? languageId = null,
        bool clearCache = true)
    {
        var localResources = new Dictionary<string, string>(resources, StringComparer.InvariantCultureIgnoreCase);
        var keys = localResources.Keys.Select(key => key.ToLowerInvariant()).ToArray();
        var resourcesToUpdate = await _localizationRepsitory.GetAllAsync(q => q.Where(p => p.LanguageId == languageId && keys.Contains(p.ResourceName.ToLower())));
        var existsResources = new List<string>();

        foreach (var localeStringResource in resourcesToUpdate.ToList())
        {
            var newValue = localResources[localeStringResource.ResourceName];

            if (localeStringResource.ResourceValue.Equals(newValue))
            {
                resourcesToUpdate.Remove(localeStringResource);
            }

            localeStringResource.ResourceValue = newValue;
            existsResources.Add(localeStringResource.ResourceName);
        }

        await _localizationRepsitory.UpdateAsync(resourcesToUpdate);

        if (clearCache)
        {
            await _staticCacheManager.RemoveByPrefixAsync(EntityCacheDefaults<LocaleStringResource>.Prefix);
        }

        return localResources
            .Where(item => !existsResources.Contains(item.Key, StringComparer.InvariantCultureIgnoreCase))
            .ToDictionary(p => p.Key, p => p.Value);
    }

    private static Dictionary<string, KeyValuePair<Guid, string>> ResourceValuesToDictionary(IEnumerable<LocaleStringResource> locales)
    {
        var dictionary = new Dictionary<string, KeyValuePair<Guid, string>>();
        foreach (var locale in locales)
        {
            var resourceName = locale.ResourceName.ToLowerInvariant();
            if (!dictionary.ContainsKey(resourceName))
            {
                dictionary.Add(resourceName, new KeyValuePair<Guid, string>(locale.Id, locale.ResourceValue));
            }
        }

        return dictionary;
    }

    #endregion
}
