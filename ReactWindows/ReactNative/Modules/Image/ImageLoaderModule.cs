﻿using Microsoft.Toolkit.Uwp.UI;
using Newtonsoft.Json.Linq;
using ReactNative.Bridge;
using System;
using System.Reactive.Linq;
using Windows.UI.Xaml.Media.Imaging;

namespace ReactNative.Modules.Image
{
    class ImageLoaderModule : NativeModuleBase
    {
        private const string ErrorInvalidUri = "E_INVALID_URI";
        private const string ErrorPrefetchFailure = "E_PREFETCH_FAILURE";
        private const string ErrorGetSizeFailure = "E_GET_SIZE_FAILURE";

        public override string Name
        {
            get
            {
                return "ImageLoader";
            }
        }

        [ReactMethod]
        public void prefetchImage(string uriString, IPromise promise)
        {
            if (string.IsNullOrEmpty(uriString))
            {
                promise.Reject(ErrorInvalidUri, "Cannot prefetch an image for an empty URI.");
                return;
            }

            DispatcherHelpers.RunOnDispatcher(async () =>
            {
                try
                {
                    // TODO: enable prefetch cancellation
                    var uri = new Uri(uriString);
                    await ImageCache.Instance.PreCacheAsync(uri, true, true).ConfigureAwait(false);
                    promise.Resolve(true);
                }
                catch (Exception ex)
                {
                    promise.Reject(ErrorPrefetchFailure, ex.Message);
                }
            });
        }

        [ReactMethod]
        public void getSize(string uriString, IPromise promise)
        {
            if (string.IsNullOrEmpty(uriString))
            {
                promise.Reject(ErrorInvalidUri, "Cannot get the size of an image for an empty URI.");
                return;
            }

            DispatcherHelpers.RunOnDispatcher(async () =>
            {
                try
                {
                    if (BitmapImageHelpers.IsHttpUri(uriString))
                    {
                        var bitmapImage = await ImageCache.Instance.GetFromCacheAsync(new Uri(uriString), true);
                        promise.Resolve(new JObject
                        {
                            { "width", bitmapImage.PixelWidth },
                            { "height", bitmapImage.PixelHeight },
                        });
                    }
                    else
                    {
                        var bitmapImage = new BitmapImage();
                        var loadQuery = bitmapImage.GetStreamLoadObservable()
                            .Where(status => status.LoadStatus == ImageLoadStatus.OnLoadEnd)
                            .FirstAsync()
                            .Replay(1);

                        using (loadQuery.Connect())
                        {
                            using (var stream = await BitmapImageHelpers.GetStreamAsync(uriString))
                            {
                                await bitmapImage.SetSourceAsync(stream);
                            }

                            await loadQuery;

                            promise.Resolve(new JObject
                            {
                                { "width", bitmapImage.PixelWidth },
                                { "height", bitmapImage.PixelHeight },
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    promise.Reject(ErrorGetSizeFailure, ex.Message);
                }
            });
        }
    }
}
