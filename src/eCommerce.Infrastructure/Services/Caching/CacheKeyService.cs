﻿using System.Globalization;
using System.Text;
using eCommerce.Core.Configuration;
using eCommerce.Core.Primitives;
using eCommerce.Core.Shared;

namespace eCommerce.Infrastructure.Services.Caching;

public abstract class CacheKeyService
{
    #region Properties

    protected string HashAlgorithm => "SHA1";

    #endregion

    #region Public Methods

    public CacheKey PrepareKey(CacheKey cacheKey, params object[] cacheKeyParameters)
    {
        return cacheKey.Create(CreateCacheKeyParameters, cacheKeyParameters);
    }

    public CacheKey PrepareKeyForDefaultCache(CacheKey cacheKey, params object[] cacheKeyParameters)
    {
        var key = cacheKey.Create(CreateCacheKeyParameters, cacheKeyParameters);

        key.CacheTime = CachingDefaults.DefaultCacheTime;

        return key;
    }

    public CacheKey PrepareKeyForShortTermCache(CacheKey cacheKey, params object[] cacheKeyParameters)
    {
        var key = cacheKey.Create(CreateCacheKeyParameters, cacheKeyParameters);

        key.CacheTime = CachingDefaults.ShortTermCacheTime;

        return key;
    }

    #endregion

    #region Methods

    protected string PrepareKeyPrefix(string prefix, params object[] prefixParameters)
    {
        return prefixParameters?.Any() ?? false
            ? string.Format(prefix, prefixParameters.Select(CreateCacheKeyParameters).ToArray())
            : prefix;
    }

    protected string CreateIdsHash(IEnumerable<Guid> guids)
    {
        var identifiers = guids.ToList();

        if (!identifiers.Any())
            return string.Empty;

        var identifiersString = string.Join(", ", identifiers.OrderBy(id => id));
        return EncryptionHelper.CreateHash(Encoding.UTF8.GetBytes(identifiersString), HashAlgorithm);
    }

    protected object CreateCacheKeyParameters(object parameter)
    {
        return parameter switch
        {
            null => "null",
            IEnumerable<Guid> guids => CreateIdsHash(guids),
            IEnumerable<BaseEntity> entities => CreateIdsHash(entities.Select(entity => entity.Id)),
            BaseEntity entity => entity.Id,
            decimal param => param.ToString(CultureInfo.InvariantCulture),
            _ => parameter
        };
    }

    #endregion
}