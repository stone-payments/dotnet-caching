using System;
using System.Threading.Tasks;
using Moq;
using NUnit.Framework;
using Shouldly;
using Vtex.Caching.Interfaces;

namespace Vtex.Caching.Tests
{
    [TestFixture("redis", Category = "Redis")]
    [TestFixture("inProcess")]
    public class RawCacheTests
    {
        private readonly IRawCache _cacheBackend;

        public RawCacheTests(string backendName)
        {
            switch (backendName)
            {
                case "redis":
                    this._cacheBackend = ResourceFactory.GetRedisCache();
                    break;
                case "inProcess":
                    this._cacheBackend = ResourceFactory.GetInProcessCache();
                    break;
            }
        }

        [Test]
        public async Task Should_set_and_get_value()
        {
            //Arrange
            var testKey = ResourceFactory.GenerateKey();

            await this._cacheBackend.SetAsync(testKey, "testValue", TimeSpan.FromSeconds(1));

            //Act
            var result = await this._cacheBackend.GetAsync<string>(testKey);

            //Assert
            result.ShouldBe("testValue");
        }

        [Test]
        public async Task Should_exist_when_set()
        {
            //Arrange
            var testKey = ResourceFactory.GenerateKey();

            await this._cacheBackend.SetAsync(testKey, "testValue", TimeSpan.FromSeconds(1));

            //Act
            var result = await this._cacheBackend.ExistsAsync(testKey);

            //Assert
            result.ShouldBe(true);
        }

        [Test]
        public async Task Should_delete_value()
        {
            //Arrange
            var testKey = ResourceFactory.GenerateKey();

            await this._cacheBackend.SetAsync(testKey, "testValue", null);

            //Act
            await this._cacheBackend.DeleteAsync(testKey);

            //Assert
            (await this._cacheBackend.GetAsync<string>(testKey)).ShouldBe(null);
        }

        [Test]
        public async Task Should_update_time_to_live()
        {
            //Arrange
            var testKey = ResourceFactory.GenerateKey();

            await this._cacheBackend.SetAsync(testKey, "testValue", null);

            //Act
            await this._cacheBackend.ExpireInAsync(testKey, TimeSpan.FromSeconds(1));

            //Assert
            (await this._cacheBackend.GetTimeToLiveAsync(testKey)).ShouldNotBe(null);
        }

        [Test]
        public async Task Should_return_null_time_to_live()
        {
            //Arrange
            var testKey = ResourceFactory.GenerateKey();

            await this._cacheBackend.SetAsync(testKey, "testValue", null);

            //Act
            var timeToLive = await this._cacheBackend.GetTimeToLiveAsync(testKey);

            //Assert
            timeToLive.ShouldBe(null);
        }

        [Test]
        public async Task Should_store_null()
        {
            //Arrange
            var testKey = ResourceFactory.GenerateKey();

            //Act
            await this._cacheBackend.SetAsync<object>(testKey, null, TimeSpan.FromSeconds(1));

            //Assert
            (await this._cacheBackend.GetAsync<object>(testKey)).ShouldBe(null);
        }

        [Test]
        public async Task Should_create_value_once()
        {
            //Arrange
            var testKey = ResourceFactory.GenerateKey();

            var fakeValueCreator = new Mock<IFakeValueCreator>();
            fakeValueCreator.Setup(it => it.CreateString()).ReturnsAsync("testValue");

            await
                this._cacheBackend.GetOrSetAsync(testKey, TimeSpan.FromSeconds(1),
                    () => fakeValueCreator.Object.CreateString());

            //Act
            await
                this._cacheBackend.GetOrSetAsync(testKey, TimeSpan.FromSeconds(1),
                    () => fakeValueCreator.Object.CreateString());

            //Assert
            fakeValueCreator.Verify(it => it.CreateString(), Times.Once);
        }
    }
}
