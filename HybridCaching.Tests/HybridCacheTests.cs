using System;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Shouldly;

namespace HybridCaching.Tests
{
    [TestFixture(Category = "Redis")]
    public class HybridCacheTests
    {
        public class GetOrSetAsync
        {
            [Test]
            public async Task Should_get_created_value()
            {
                //Arrange
                var hybridCache = ResourceFactory.GetHybridCache();
                var testKey = ResourceFactory.GenerateKey();

                var fakeValueCreator = new Mock<IFakeValueCreator>();
                fakeValueCreator.Setup(it => it.CreateString()).ReturnsAsync("testValue");

                //Act
                var value = await hybridCache.GetOrSetAsync(testKey, TimeSpan.FromSeconds(1),
                    () => fakeValueCreator.Object.CreateString());

                //Assert
                value.ShouldBe("testValue");
            }

            [Test]
            public async Task Should_create_value_once()
            {
                //Arrange
                var hybridCache = ResourceFactory.GetHybridCache();
                var testKey = ResourceFactory.GenerateKey();

                var fakeValueCreator = new Mock<IFakeValueCreator>();
                fakeValueCreator.Setup(it => it.CreateString()).ReturnsAsync("testValue");

                await hybridCache.GetOrSetAsync(testKey, TimeSpan.FromSeconds(1),
                    () => fakeValueCreator.Object.CreateString());

                //Act
                await hybridCache.GetOrSetAsync(testKey, TimeSpan.FromSeconds(1),
                    () => fakeValueCreator.Object.CreateString());

                //Assert
                fakeValueCreator.Verify(it => it.CreateString(), Times.Once);
            }

            [Test]
            public async Task Should_create_many()
            {
                //Arrange
                var hybridCache = ResourceFactory.GetHybridCache();
                var testDictionary = ResourceFactory.GenerateDictionary(10);

                var fakeValueCreator = new Mock<IFakeValueCreator>();
                fakeValueCreator.Setup(it => it.CreateDictionary()).ReturnsAsync(testDictionary);

                //Act
                var firstValue = await hybridCache.GetOrSetAsync(testDictionary.First().Key, TimeSpan.FromSeconds(1),
                            () => fakeValueCreator.Object.CreateDictionary());

                var lastValue = await hybridCache.GetOrSetAsync(testDictionary.Last().Key, TimeSpan.FromSeconds(1),
                            () => fakeValueCreator.Object.CreateDictionary());

                //Assert
                firstValue.ShouldBe(testDictionary.First().Value);
                lastValue.ShouldBe(testDictionary.Last().Value);
                fakeValueCreator.Verify(it => it.CreateDictionary(), Times.Once);
                fakeValueCreator.Verify(it => it.CreateString(), Times.Never);
            }

            [Test]
            public async Task Should_cache_null()
            {
                //Arrange
                var hybridCache = ResourceFactory.GetHybridCache();
                var testKey = ResourceFactory.GenerateKey();

                var fakeValueCreator = new Mock<IFakeValueCreator>();
                fakeValueCreator.Setup(it => it.CreateString()).ReturnsAsync("testValue");

                await hybridCache.SetAsync(testKey, (string)null, TimeSpan.FromSeconds(1));

                //Act
                var value = await hybridCache.GetOrSetAsync(testKey, TimeSpan.FromSeconds(1),
                            () => fakeValueCreator.Object.CreateString());

                //Assert
                value.ShouldBe(null);
                fakeValueCreator.Verify(it => it.CreateString(), Times.Never);
            }

            [Test]
            public async Task Should_set_all_backends()
            {
                //Arrange
                var redisCache = ResourceFactory.GetRedisCache();
                var inProcessCache = ResourceFactory.GetInProcessCache();
                var hybridCache = ResourceFactory.GetHybridCache(redisCache, inProcessCache);
                var testKey = ResourceFactory.GenerateKey();

                //Act
                await hybridCache.GetOrSetAsync(testKey, TimeSpan.FromSeconds(1), () => Task.FromResult("testValue"));

                //Assert
                (await redisCache.GetAsync<string>(testKey)).ShouldBe("testValue");
                (await inProcessCache.GetAsync<string>(testKey)).ShouldBe("testValue");
            }

            [Test]
            public async Task Should_propagate_cache()
            {
                //Arrange
                var redisCache = ResourceFactory.GetRedisCache();
                var inProcessCache = ResourceFactory.GetInProcessCache();
                var hybridCache = ResourceFactory.GetHybridCache(redisCache, inProcessCache);
                var testKey = ResourceFactory.GenerateKey();

                var fakeValueCreator = new Mock<IFakeValueCreator>();
                fakeValueCreator.Setup(it => it.CreateString()).ReturnsAsync("generatedValue");

                await redisCache.SetAsync(testKey, "cachedValue", TimeSpan.FromSeconds(1));

                //Act
                var value = await hybridCache.GetOrSetAsync(testKey, TimeSpan.FromSeconds(1),
                            () => fakeValueCreator.Object.CreateString());

                //Assert
                value.ShouldBe("cachedValue");
                (await inProcessCache.GetAsync<string>(testKey)).ShouldBe("cachedValue");
                fakeValueCreator.Verify(it => it.CreateString(), Times.Never);
            }
        }

        public class Subscribe
        {
            [Test]
            public async Task Should_propagate_delete()
            {
                //Arrange
                var redisCache = ResourceFactory.GetRedisCache();
                var inProcessCache = ResourceFactory.GetInProcessCache();
                var hybridCache = ResourceFactory.GetHybridCache(redisCache, inProcessCache);
                var testKey = ResourceFactory.GenerateKey();

                await hybridCache.SetAsync(testKey, "testValue", TimeSpan.FromSeconds(1));

                //Act
                await redisCache.DeleteAsync(testKey);

                await Task.Delay(500);

                //Assert
                (await inProcessCache.GetAsync<string>(testKey)).ShouldBe(null);
            }

            [Test]
            public async Task Should_propagate_expire_update()
            {
                //Arrange
                var redisCache = ResourceFactory.GetRedisCache();
                var inProcessCache = ResourceFactory.GetInProcessCache();
                var hybridCache = ResourceFactory.GetHybridCache(redisCache, inProcessCache);
                var testKey = ResourceFactory.GenerateKey();

                await hybridCache.SetAsync(testKey, "testValue", TimeSpan.FromSeconds(1));

                //Act
                await redisCache.ExpireInAsync(testKey, TimeSpan.FromSeconds(2));

                await Task.Delay(1000);

                //Assert
                (await inProcessCache.GetAsync<string>(testKey)).ShouldBe("testValue");
            }

            [Test]
            public async Task Should_propagate_expired()
            {
                //Arrange
                var redisCache = ResourceFactory.GetRedisCache();
                var inProcessCache = ResourceFactory.GetInProcessCache();
                var hybridCache = ResourceFactory.GetHybridCache(redisCache, inProcessCache);
                var testKey = ResourceFactory.GenerateKey();

                await hybridCache.SetAsync(testKey, "testValue", null);

                //Act
                await redisCache.ExpireInAsync(testKey, TimeSpan.Zero);

                await Task.Delay(500);

                //Assert
                (await inProcessCache.GetAsync<string>(testKey)).ShouldBe(null);
            }
        }
    }
}
