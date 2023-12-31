﻿using System.Linq.Expressions;
using System.Reflection;
using eCommerce.Core.Primitives;
using eCommerce.Core.Entities.Configuration.CustomSettings;
using eCommerce.Core.Entities.Localization;
using eCommerce.Core.Services.Caching;
using eCommerce.Core.Shared;
using eCommerce.Core.Services.Localization;
using eCommerce.Infrastructure.Concretes;
using eCommerce.Core.Interfaces;
using eCommerce.Infrastructure.Extensions;

namespace eCommerce.Infrastructure.Services.Localization;

public class LocalizedEntityService : ILocalizedEntityService
{
    #region Fields

    private readonly IStaticCacheManager _staticCacheManager;

    private readonly LocalizationSettings _localizationSettings;

    private readonly IRepository<LocalizedProperty> _localizedPropertyRepository;

    #endregion

    #region Constructure and Destructure

    public LocalizedEntityService(
        IStaticCacheManager staticCacheManager,
        LocalizationSettings localizationSettings,
        IRepository<LocalizedProperty> localizedPropertyRepository)
    {
        _staticCacheManager = staticCacheManager;
        _localizationSettings = localizationSettings;
        _localizedPropertyRepository = localizedPropertyRepository;
    }

    #endregion

    #region Methods

    private async Task<IList<LocalizedProperty>> GetLocalizedPropertiesAsync(Guid entityId, string localeKeyGroup)
    {
        if (string.IsNullOrEmpty(localeKeyGroup))
        {
            return new List<LocalizedProperty>();
        }

        return await _localizedPropertyRepository.GetAllAsync(q => q.Where(p => p.EntityId == entityId && p.LocaleKeyGroup == localeKeyGroup));
    }

    private async Task<IList<LocalizedProperty>> GetAllLocalizedPropertiesAsync()
    {
        return await _localizedPropertyRepository.GetAllAsync(getCacheKey: q => q.PrepareKeyForDefaultCache(EntityCacheDefaults<LocalizedProperty>.AllCacheKey));
    }

    private async Task<IList<LocalizedProperty>> GetAllLocalizedPropertiesAsync(Guid languageId)
    {
        return await _localizedPropertyRepository.GetAllAsync(q => q.Where(p => p.LanguageId == languageId));
    }

    private async Task DeleteLocalizedPropertyAsync(LocalizedProperty localizedProperty) => await _localizedPropertyRepository.DeleteAsync(localizedProperty);

    private async Task InsertLocalizedPropertyAsync(LocalizedProperty localizedProperty) => await _localizedPropertyRepository.InsertAsync(localizedProperty);

    private async Task UpdateLocalizedPropertyAsync(LocalizedProperty localizedProperty) => await _localizedPropertyRepository.UpdateAsync(localizedProperty);

    #endregion

    #region Methods

    public async Task<IList<LocalizedProperty>> GetEntityLocalizedPropertiesAsync(
        Guid entityId,
        string localeKeyGroup,
        string localeKey)
    {
        var key = _staticCacheManager.PrepareKeyForDefaultCache(
            LocalizationDefaults.LocalizedPropertiesCacheKey,
            entityId,
            localeKeyGroup,
            localeKey);

        return await _staticCacheManager.GetAsync(key, async () =>
        {
            var source = _localizationSettings.LoadAllLocalizedPropertiesOnStartup
                ? (await GetAllLocalizedPropertiesAsync()).AsQueryable() : _localizedPropertyRepository.Table;

            var query = from lp in source
                        where lp.EntityId == entityId &&
                              lp.LocaleKeyGroup == localeKeyGroup &&
                              lp.LocaleKey == localeKey
                        select lp;

            return await query.ToListAsync();
        });
    }

    public async Task<string> GetLocalizedValueAsync(
        Guid languageId,
        Guid entityId,
        string localeKeyGroup,
        string localeKey)
    {
        var key = _staticCacheManager.PrepareKeyForDefaultCache(
            LocalizationDefaults.LocalizedPropertyCacheKey,
            languageId,
            entityId,
            localeKeyGroup,
            localeKey);

        return await _staticCacheManager.GetAsync(key, async () =>
        {
            if (_localizationSettings.LoadAllLocalizedPropertiesOnStartup)
            {
                var lookupKey = _staticCacheManager.PrepareKeyForDefaultCache(
                    LocalizationDefaults.LocalizedPropertyLookupCacheKey,
                    languageId);
                var lookup = await _staticCacheManager.GetAsync(
                    lookupKey,
                    async () => (await GetAllLocalizedPropertiesAsync(languageId))
                        .ToGroupedDictionary(p => p.EntityId));

                return lookup.TryGetValue(entityId, out var localizedProperties)
                    ? localizedProperties.FirstOrDefault(p => p.LocaleKeyGroup == localeKeyGroup && p.LocaleKey == localeKey)
                        ?.LocaleValue ?? string.Empty
                    : string.Empty;
            }

            return (await _localizedPropertyRepository.GetFirstOrDefaultAsync(
                func: q => q.Where(p => p.LanguageId == languageId &&
                                        p.EntityId == entityId &&
                                        p.LocaleKeyGroup == localeKeyGroup &&
                                        p.LocaleKey == localeKey))).LocaleValue;
        });
    }

    public async Task SaveLocalizedValueAsync<T>(
        T entity,
        Expression<Func<T, string>> keySelector,
        string localeValue,
        Guid languageId) where T : BaseEntity, ILocalizedEntity
    {
        await SaveLocalizedValueAsync<T, string>(entity, keySelector, localeValue, languageId);
    }

    public async Task SaveLocalizedValueAsync<T, TPropType>(
        T entity,
        Expression<Func<T, TPropType>> keySelector,
        TPropType localeValue,
        Guid languageId) where T : BaseEntity, ILocalizedEntity
    {
        if (entity is null)
            throw new ArgumentNullException(nameof(entity));

        if (keySelector.Body is not MemberExpression member)
        {
            throw new ArgumentException(string.Format(
                "Expression '{0}' refers to a method, not a property.",
                keySelector));
        }

        var propInfo = member.Member as PropertyInfo;
        if (propInfo == null)
        {
            throw new ArgumentException(string.Format(
                   "Expression '{0}' refers to a field, not a property.",
                   keySelector));
        }

        //load localized value (check whether it's a cacheable entity. In such cases we load its original entity type)
        var localeKeyGroup = entity.GetType().Name;
        var localeKey = propInfo.Name;

        var props = await GetLocalizedPropertiesAsync(entity.Id, localeKeyGroup);
        var prop = props.FirstOrDefault(lp => lp.LanguageId == languageId &&
            lp.LocaleKey.Equals(localeKey, StringComparison.InvariantCultureIgnoreCase)); //should be culture invariant

        var localeValueStr = CommonHelper.To<string>(localeValue);

        if (prop != null)
        {
            if (string.IsNullOrWhiteSpace(localeValueStr))
            {
                //delete
                await DeleteLocalizedPropertyAsync(prop);
            }
            else
            {
                //update
                prop.LocaleValue = localeValueStr;
                await UpdateLocalizedPropertyAsync(prop);
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(localeValueStr))
                return;

            //insert
            prop = new LocalizedProperty
            {
                EntityId = entity.Id,
                LanguageId = languageId,
                LocaleKey = localeKey,
                LocaleKeyGroup = localeKeyGroup,
                LocaleValue = localeValueStr
            };
            await InsertLocalizedPropertyAsync(prop);
        }
    }

    #endregion
}