namespace AgendaBuddy.Library.Showcase;

/// <summary>
/// Every showcase write is a targeted <c>FindOneAndUpdate</c> whose filter carries the rule (ADR-032), so the cap,
/// the duplicate check and the permutation check are atomic with the write they guard.
/// </summary>
public sealed class ShowcaseService(
    IRepository<ProviderShowcaseEntity> showcases,
    IRepository<MediaRefEntity> mediaRefs,
    IRepository<ShowcaseVisitEntity> visits,
    IRepository<GoCounterEntity> goCounters,
    IRepository<ShowcaseReportEntity> reports,
    IRepository<ShowcaseBlockEntity> blocks,
    IRepository<ProviderEntity> providers,
    IRepository<CustomerEntity> customers,
    IRepository<AppointmentEntity> appointments,
    TimeProvider timeProvider)
    : IShowcaseService
{
    public const string PhotoField = "photo_hash";
    public const string LogoField = "logo_hash";
    private const int MaxCodeAttempts = 5;

    private DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

    public Task<ProviderEntity?> FindProviderByEmailAsync(string email) =>
        providers.FindOneAsync(SupportTools<ProviderEntity>.FilterByEmail(email));

    public async Task<ProviderEntity?> FindProviderByRefAsync(string? providerRef) =>
        ObjectId.TryParse(providerRef, out var id)
            ? await providers.FindOneAsync(new BsonDocument("_id", id))
            : null;

    public Task<ProviderShowcaseEntity?> FindShowcaseAsync(ObjectId providerId) =>
        showcases.FindOneAsync(new BsonDocument("provider_id", providerId));

    public async Task<ProviderShowcaseEntity?> FindShowcaseByCodeAsync(string code)
    {
        var normalised = PublicCodeGenerator.Normalise(code);
        return normalised is null ? null : await showcases.FindOneAsync(new BsonDocument("public_code", normalised));
    }

    public async Task<ProviderShowcaseEntity> EnsureShowcaseAsync(ProviderEntity provider)
    {
        var existing = await FindShowcaseAsync(provider.Id);
        if (existing is not null)
            return existing;

        var created = new ProviderShowcaseEntity
        {
            Id = ObjectId.GenerateNewId(),
            ProviderId = provider.Id,
            ProviderEmail = provider.Email,
            UpdatedAt = UtcNow
        };

        try
        {
            await showcases.InsertAsync(created);
            return created;
        }
        catch (MongoException ex) when (IsDuplicateKey(ex))
        {
            return await FindShowcaseAsync(provider.Id)
                   ?? throw new InvalidOperationException("Showcase vanished between insert and read.", ex);
        }
    }

    public async Task<ProviderShowcaseEntity> SetTextAsync(ProviderEntity provider, string? tagline, string? about)
    {
        await EnsureShowcaseAsync(provider);

        var updated = await showcases.FindOneAndUpdateAsync(
            ByProvider(provider.Id),
            new BsonDocument("$set", new BsonDocument
            {
                { "tagline", ToBson(tagline) },
                { "about", ToBson(about) },
                { "updated_at", UtcNow }
            }));

        return updated ?? await EnsureShowcaseAsync(provider);
    }

    public async Task<ShowcaseWriteResult> SetImageAsync(ProviderEntity provider, string field, string? hash)
    {
        if (field is not (PhotoField or LogoField))
            throw new ArgumentOutOfRangeException(nameof(field), field, "Only the photo and the logo are single images.");

        if (hash is not null && await FindOwnedMediaAsync(provider.Id, hash) is null)
            return new ShowcaseWriteResult(ShowcaseWriteStatus.UnknownMedia);

        var before = await EnsureShowcaseAsync(provider);
        var previous = field == PhotoField ? before.PhotoHash : before.LogoHash;

        var updated = await showcases.FindOneAndUpdateAsync(
            ByProvider(provider.Id),
            new BsonDocument("$set", new BsonDocument { { field, ToBson(hash) }, { "updated_at", UtcNow } }));

        if (hash is not null)
            await AttachAsync(provider.Id, hash);
        if (previous is not null && previous != hash)
            await DetachIfUnusedAsync(provider.Id, previous);

        return new ShowcaseWriteResult(ShowcaseWriteStatus.Ok, updated);
    }

    public async Task<ShowcaseWriteResult> AddPortfolioItemAsync(
        ProviderEntity provider, string hash, string? caption, string? serviceId)
    {
        var media = await FindOwnedMediaAsync(provider.Id, hash);
        if (media is null)
            return new ShowcaseWriteResult(ShowcaseWriteStatus.UnknownMedia);

        if (!TryResolveService(provider, serviceId, out var service))
            return new ShowcaseWriteResult(ShowcaseWriteStatus.InvalidService);

        await EnsureShowcaseAsync(provider);

        var now = UtcNow;
        var item = new PortfolioItem
        {
            Hash = hash,
            Caption = caption,
            ServiceId = service,
            Width = media.Width,
            Height = media.Height,
            AddedAt = now
        };

        var filter = new BsonDocument
        {
            { "provider_id", provider.Id },
            { "portfolio.hash", new BsonDocument("$ne", hash) },
            {
                "$expr", new BsonDocument("$lt", new BsonArray
                {
                    new BsonDocument("$size", "$portfolio"), ShowcaseRules.MaxPortfolioItems
                })
            }
        };

        var updated = await showcases.FindOneAndUpdateAsync(filter, new BsonDocument
        {
            { "$push", new BsonDocument("portfolio", item.ToBsonDocument()) },
            { "$set", new BsonDocument { { "updated_at", now }, { "portfolio_changed_at", now } } }
        });

        if (updated is not null)
        {
            await AttachAsync(provider.Id, hash);
            return new ShowcaseWriteResult(ShowcaseWriteStatus.Ok, updated, item);
        }

        // The filter refused, and one read tells an idempotent retry from a full portfolio.
        var current = await EnsureShowcaseAsync(provider);
        var existing = current.Portfolio.FirstOrDefault(p => p.Hash == hash);
        return existing is not null
            ? new ShowcaseWriteResult(ShowcaseWriteStatus.AlreadyPresent, current, existing)
            : new ShowcaseWriteResult(ShowcaseWriteStatus.PortfolioFull, current);
    }

    public async Task<ShowcaseWriteResult> UpdatePortfolioItemAsync(
        ProviderEntity provider, string hash, string? caption, string? serviceId)
    {
        if (!TryResolveService(provider, serviceId, out var service))
            return new ShowcaseWriteResult(ShowcaseWriteStatus.InvalidService);

        var updated = await showcases.FindOneAndUpdateAsync(
            new BsonDocument { { "provider_id", provider.Id }, { "portfolio.hash", hash } },
            new BsonDocument("$set", new BsonDocument
            {
                { "portfolio.$.caption", ToBson(caption) },
                { "portfolio.$.service_id", service.HasValue ? service.Value : BsonNull.Value },
                { "updated_at", UtcNow }
            }));

        var item = updated?.Portfolio.FirstOrDefault(p => p.Hash == hash);
        return item is null
            ? new ShowcaseWriteResult(ShowcaseWriteStatus.NotFound)
            : new ShowcaseWriteResult(ShowcaseWriteStatus.Ok, updated, item);
    }

    public async Task RemovePortfolioItemAsync(ProviderEntity provider, string hash)
    {
        var updated = await showcases.FindOneAndUpdateAsync(
            new BsonDocument { { "provider_id", provider.Id }, { "portfolio.hash", hash } },
            new BsonDocument
            {
                { "$pull", new BsonDocument("portfolio", new BsonDocument("hash", hash)) },
                { "$set", new BsonDocument("updated_at", UtcNow) }
            });

        if (updated is not null)
            await DetachIfUnusedAsync(provider.Id, hash);
    }

    public async Task<ShowcaseWriteResult> ReorderPortfolioAsync(ProviderEntity provider, IReadOnlyList<string> hashes)
    {
        var current = await EnsureShowcaseAsync(provider);
        var byHash = current.Portfolio.ToDictionary(p => p.Hash, StringComparer.Ordinal);

        if (hashes.Count != byHash.Count
            || hashes.Distinct(StringComparer.Ordinal).Count() != hashes.Count
            || !hashes.All(byHash.ContainsKey))
            return new ShowcaseWriteResult(ShowcaseWriteStatus.PortfolioChanged, current);

        var rebuilt = new BsonArray(hashes.Select(h => byHash[h].ToBsonDocument()));

        // Re-validated in the filter: another device may have changed the set since the read above.
        var filter = new BsonDocument
        {
            { "provider_id", provider.Id },
            { "portfolio", new BsonDocument("$size", hashes.Count) }
        };
        if (hashes.Count > 0)
            filter.Add("portfolio.hash", new BsonDocument("$all", new BsonArray(hashes)));

        var updated = await showcases.FindOneAndUpdateAsync(filter,
            new BsonDocument("$set", new BsonDocument { { "portfolio", rebuilt }, { "updated_at", UtcNow } }));

        return updated is null
            ? new ShowcaseWriteResult(ShowcaseWriteStatus.PortfolioChanged, current)
            : new ShowcaseWriteResult(ShowcaseWriteStatus.Ok, updated);
    }

    public async Task<string> GetOrCreatePublicCodeAsync(ProviderEntity provider)
    {
        var showcase = await EnsureShowcaseAsync(provider);
        if (showcase.PublicCode is not null)
        {
            await EnsureGoCounterAsync(showcase.PublicCode, provider.Id);
            return showcase.PublicCode;
        }

        for (var attempt = 0; attempt < MaxCodeAttempts; attempt++)
        {
            var candidate = PublicCodeGenerator.Next();
            try
            {
                var updated = await showcases.FindOneAndUpdateAsync(
                    new BsonDocument
                    {
                        { "provider_id", provider.Id },
                        { "public_code", new BsonDocument("$exists", false) }
                    },
                    new BsonDocument("$set", new BsonDocument("public_code", candidate)));

                // Null means a concurrent call won; its code is the one to return.
                var code = updated?.PublicCode ?? (await FindShowcaseAsync(provider.Id))?.PublicCode;
                if (code is not null)
                {
                    await EnsureGoCounterAsync(code, provider.Id);
                    return code;
                }
            }
            catch (MongoException ex) when (IsDuplicateKey(ex))
            {
                // Collided with another provider's code; draw again.
            }
        }

        throw new InvalidOperationException($"Could not allocate a unique public code in {MaxCodeAttempts} attempts.");
    }

    public ShowcaseCompleteness Completeness(ProviderShowcaseEntity showcase)
    {
        var missing = new List<string>();
        if (showcase.PhotoHash is null) missing.Add("photo");
        if (showcase.LogoHash is null) missing.Add("logo");
        if (showcase.Tagline is null) missing.Add("tagline");
        if (showcase.About is null) missing.Add("about");
        if (showcase.Portfolio.Count < 3) missing.Add("portfolio");
        const int total = 5;
        return new ShowcaseCompleteness(total - missing.Count, total, missing);
    }

    public async Task<ShowcaseFunnel> GetFunnelAsync(ProviderEntity provider, ProviderShowcaseEntity showcase)
    {
        long scans = 0;
        if (showcase.PublicCode is not null)
        {
            var counter = await goCounters.FindOneAsync(new BsonDocument("code", showcase.PublicCode));
            scans = counter is null ? 0 : counter.Ios + counter.Android + counter.Other;
        }

        var attributed = (await visits.FindAllAsync(new BsonDocument
        {
            { "provider_id", provider.Id },
            { "first_source", new BsonDocument("$in", new BsonArray { ShowcaseSources.Scan, ShowcaseSources.Code }) }
        })).ToList();

        long booked = 0;
        if (attributed.Count > 0)
        {
            var booking = (await appointments.FindAllAsync(new BsonDocument
            {
                { "email_provider", provider.Email },
                { "email_customer", new BsonDocument("$in", new BsonArray(attributed.Select(v => v.CustomerEmail))) }
            })).ToList();

            // An appointment's creation time is its ObjectId's timestamp; the entity records no other.
            booked = attributed.Count(visit => booking.Any(appointment =>
                string.Equals(appointment.EmailCustomer, visit.CustomerEmail, StringComparison.OrdinalIgnoreCase)
                && appointment.Id.CreationTime >= visit.FirstSeenAt.AddSeconds(-1)
                && appointment.Id.CreationTime <= visit.FirstSeenAt + ShowcaseRules.FunnelBookingWindow));
        }

        return new ShowcaseFunnel(scans, attributed.Count, booked, (int)ShowcaseRules.FunnelBookingWindow.TotalDays);
    }

    public async Task<DateTime?> RecordVisitAsync(ObjectId providerId, string customerEmail, string source)
    {
        var now = UtcNow;
        var key = new BsonDocument { { "provider_id", providerId }, { "customer_email", customerEmail } };
        var existing = await visits.FindOneAsync(key);

        if (existing is null)
        {
            try
            {
                await visits.InsertAsync(new ShowcaseVisitEntity
                {
                    Id = ObjectId.GenerateNewId(),
                    ProviderId = providerId,
                    CustomerEmail = customerEmail,
                    FirstSource = source,
                    FirstSeenAt = now,
                    LastSeenAt = now,
                    VisitCount = 1,
                    SourceCounts = new Dictionary<string, int> { [source] = 1 }
                });
                return null;
            }
            catch (MongoException ex) when (IsDuplicateKey(ex))
            {
                return now;
            }
        }

        var debounced = new BsonDocument(key) { { "last_seen_at", new BsonDocument("$lt", now - ShowcaseRules.VisitDebounce) } };
        await visits.FindOneAndUpdateAsync(debounced, new BsonDocument
        {
            { "$set", new BsonDocument("last_seen_at", now) },
            { "$inc", new BsonDocument { { "visit_count", 1 }, { $"source_counts.{source}", 1 } } }
        });

        return existing.LastSeenAt;
    }

    public Task<ShowcaseRelationship> GetRelationshipAsync(ProviderEntity provider, string callerEmail)
    {
        var now = UtcNow;
        var isSelf = IsOwner(provider, callerEmail);
        var mine = provider.AppointmentEntities
            .Where(a => !a.DayOff && string.Equals(a.EmailCustomer, callerEmail, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var next = mine
            .Where(a => a.Start > now && a.AppointmentStatus is AppointmentStatus.Requested or AppointmentStatus.Booked)
            .MinBy(a => a.Start);

        return Task.FromResult(new ShowcaseRelationship(
            isSelf,
            provider.SubscribedCustomerCollection.Contains(callerEmail, StringComparer.OrdinalIgnoreCase),
            next,
            mine.Any(a => a.AppointmentStatus != AppointmentStatus.Cancelled)));
    }

    public async Task<bool> IsBlockedAsync(string customerEmail, ObjectId providerId) =>
        await blocks.FindOneAsync(new BsonDocument { { "customer_email", customerEmail }, { "provider_id", providerId } })
            is not null;

    public async Task<IReadOnlyList<ObjectId>> GetBlockedProviderIdsAsync(string customerEmail) =>
        (await blocks.FindAllAsync(new BsonDocument("customer_email", customerEmail)))
        .Select(b => b.ProviderId)
        .ToList();

    public async Task BlockAsync(string customerEmail, ObjectId providerId)
    {
        if (await IsBlockedAsync(customerEmail, providerId))
            return;

        try
        {
            await blocks.InsertAsync(new ShowcaseBlockEntity
            {
                Id = ObjectId.GenerateNewId(),
                CustomerEmail = customerEmail,
                ProviderId = providerId,
                CreatedAt = UtcNow
            });
        }
        catch (MongoException ex) when (IsDuplicateKey(ex))
        {
            // A concurrent hide already recorded it.
        }
    }

    public Task UnblockAsync(string customerEmail, ObjectId providerId) =>
        blocks.DeleteManyAsync(new BsonDocument { { "customer_email", customerEmail }, { "provider_id", providerId } });

    public async Task<IReadOnlyList<HiddenProvider>> GetHiddenProvidersAsync(string customerEmail)
    {
        var ids = await GetBlockedProviderIdsAsync(customerEmail);
        if (ids.Count == 0)
            return [];

        var found = await providers.FindAllAsync(new BsonDocument("_id", new BsonDocument("$in", new BsonArray(ids))));
        return found
            .Select(p => new HiddenProvider(p.Id.ToString(), p.FirstName, p.LastName))
            .OrderBy(p => p.FirstName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<bool> ReportAsync(
        ObjectId providerId, string reporterEmail, string reason, string? detail, string? portfolioHash)
    {
        var now = UtcNow;
        var recent = await reports.FindOneAsync(new BsonDocument
        {
            { "reporter_email", reporterEmail },
            { "provider_id", providerId },
            { "created_at", new BsonDocument("$gt", now - ShowcaseRules.ReportDebounce) }
        });
        if (recent is not null)
            return false;

        await reports.InsertAsync(new ShowcaseReportEntity
        {
            Id = ObjectId.GenerateNewId(),
            ProviderId = providerId,
            ReporterEmail = reporterEmail,
            Reason = reason,
            Detail = detail,
            PortfolioHash = portfolioHash,
            CreatedAt = now
        });
        return true;
    }

    public async Task<IReadOnlyList<ShowcaseAvatarLookup>> LookupAsync(
        string callerEmail, bool callerIsProvider, IEnumerable<string> emails)
    {
        var requested = emails
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (requested.Count == 0)
            return [];

        // Only the caller's own counterparties resolve; anything else is omitted exactly like an unknown
        // address, so the route cannot tell anybody which addresses belong to providers.
        var counterparties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (callerIsProvider)
        {
            counterparties.Add(callerEmail);
        }
        else
        {
            foreach (var appointment in await appointments.FindAllAsync(new BsonDocument("email_customer", callerEmail)))
                counterparties.Add(appointment.EmailProvider);

            var customer = await customers.FindOneAsync(SupportTools<CustomerEntity>.FilterByEmail(callerEmail));
            foreach (var provider in customer?.SubscribedProviderCollection ?? [])
                counterparties.Add(provider);
        }

        var allowed = requested.Where(counterparties.Contains).ToList();
        if (allowed.Count == 0)
            return [];

        var blocked = callerIsProvider ? [] : (await GetBlockedProviderIdsAsync(callerEmail)).ToHashSet();
        var matched = (await providers.FindAllAsync(new BsonDocument("email", new BsonDocument("$in", new BsonArray(allowed)))))
            .Where(p => p.IsActive && !blocked.Contains(p.Id))
            .ToList();
        if (matched.Count == 0)
            return [];

        var entries = await GetDirectoryEntriesAsync(matched.Select(p => p.Id));
        return matched
            .Select(p => entries.TryGetValue(p.Id, out var entry)
                ? new ShowcaseAvatarLookup(p.Email, p.Id.ToString(), entry.PhotoHash, entry.PortfolioChangedAt)
                : new ShowcaseAvatarLookup(p.Email, p.Id.ToString(), null, null))
            .ToList();
    }

    public async Task<IReadOnlyDictionary<ObjectId, ShowcaseDirectoryEntry>> GetDirectoryEntriesAsync(
        IEnumerable<ObjectId> providerIds)
    {
        var ids = providerIds.Distinct().ToList();
        if (ids.Count == 0)
            return new Dictionary<ObjectId, ShowcaseDirectoryEntry>();

        var found = await showcases.FindAllAsync(new BsonDocument("provider_id", new BsonDocument("$in", new BsonArray(ids))));
        return found
            .Where(s => s.HiddenByOperator != true)
            .ToDictionary(s => s.ProviderId, s => new ShowcaseDirectoryEntry(s.PhotoHash, s.PortfolioChangedAt));
    }

    public async Task IncrementGoCounterAsync(string? code, string platform)
    {
        var normalised = PublicCodeGenerator.Normalise(code);
        if (normalised is null || platform is not ("ios" or "android" or "other"))
            return;

        await goCounters.FindOneAndUpdateAsync(
            new BsonDocument("code", normalised),
            new BsonDocument("$inc", new BsonDocument(platform, 1L)));
    }

    public async Task<bool> IsVisibleToAsync(ProviderEntity provider, ProviderShowcaseEntity? showcase, string callerEmail)
    {
        if (IsOwner(provider, callerEmail))
            return true;
        if (!provider.IsActive || showcase?.HiddenByOperator == true)
            return false;
        return !await IsBlockedAsync(callerEmail, provider.Id);
    }

    public static bool IsOwner(ProviderEntity provider, string callerEmail) =>
        string.Equals(provider.Email, callerEmail, StringComparison.OrdinalIgnoreCase);

    public static bool IsDuplicateKey(MongoException ex) => ex switch
    {
        MongoWriteException write => write.WriteError?.Category == ServerErrorCategory.DuplicateKey,
        MongoCommandException command => command.Code == 11000,
        _ => false
    };

    private Task<MediaRefEntity?> FindOwnedMediaAsync(ObjectId providerId, string hash) =>
        mediaRefs.FindOneAsync(new BsonDocument { { "provider_id", providerId }, { "hash", hash } });

    private Task AttachAsync(ObjectId providerId, string hash) =>
        mediaRefs.UpdateManyAsync(
            new BsonDocument { { "provider_id", providerId }, { "hash", hash } },
            new BsonDocument("$set", new BsonDocument { { "attached", true }, { "detached_at", BsonNull.Value } }));

    private async Task DetachIfUnusedAsync(ObjectId providerId, string hash)
    {
        var showcase = await FindShowcaseAsync(providerId);
        if (showcase is not null
            && (showcase.PhotoHash == hash || showcase.LogoHash == hash || showcase.Portfolio.Any(p => p.Hash == hash)))
            return;

        await mediaRefs.UpdateManyAsync(
            new BsonDocument { { "provider_id", providerId }, { "hash", hash } },
            new BsonDocument("$set", new BsonDocument { { "attached", false }, { "detached_at", UtcNow } }));
    }

    private async Task EnsureGoCounterAsync(string code, ObjectId providerId)
    {
        if (await goCounters.FindOneAsync(new BsonDocument("code", code)) is not null)
            return;

        try
        {
            await goCounters.InsertAsync(new GoCounterEntity { Id = ObjectId.GenerateNewId(), Code = code, ProviderId = providerId });
        }
        catch (MongoException ex) when (IsDuplicateKey(ex))
        {
            // Created concurrently.
        }
    }

    /// <summary>
    /// A service link must name one of the provider's own services. <c>ObjectId.Empty</c> never qualifies: the
    /// whole-document profile <c>PUT</c> resets nested ids to it, so it would match every such service at once.
    /// </summary>
    private static bool TryResolveService(ProviderEntity provider, string? serviceId, out ObjectId? resolved)
    {
        resolved = null;
        if (string.IsNullOrWhiteSpace(serviceId))
            return true;

        if (!ObjectId.TryParse(serviceId, out var id) || id == ObjectId.Empty
            || provider.ServiceEntities.All(s => s.Id != id))
            return false;

        resolved = id;
        return true;
    }

    private static BsonDocument ByProvider(ObjectId providerId) => new("provider_id", providerId);

    private static BsonValue ToBson(string? value) => value is null ? BsonNull.Value : new BsonString(value);
}
