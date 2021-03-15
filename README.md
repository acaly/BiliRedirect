# BiliRedirect

Bilibili的视频分P地址使用分P的序号（`?p=`）来定位，但是分P的顺序可以被UP随时修改，导致指向某一个分P的链接失效（指向同合集的另一个分P）。

这个是一个用C#语言写的超简单的HTTP服务器，负责从BVID和分P的cid（这两个参数不受分P顺序调整的影响）给出最新的分P的序号和正确的B站视频地址。

例如这个视频

```
https://www.bilibili.com/video/BV1ms411Z7N3?p=2
```

的BVID为`BV1ms411Z7N3`，从分享的嵌入代码中查到cid为`797315`。因此访问

```
/?bvid=BV1ms411Z7N3&cid=797315
```

即可进入一个重定向页面，访问者会自动被重定向到这个cid对应的分P。

---

Videos on bilibili.com have parts. Links to parts use the index parameter (`?p=`).
Because the author can insert and exchange parts in a video collection, there is no
permanent url for a specific part.

This simple HTTP server writter in C# can be used to calculate the `?p=` parameter
give the BVID and cid of a part, which will not change when the author changes the
order. This allows one to generate a url for a specific part.

For example, this video (part)

```
https://www.bilibili.com/video/BV1ms411Z7N3?p=2
```

has a BVID of `BV1ms411Z7N3`, and, from the share code, a cid of `797315`. Visit

```
/?bvid=BV1ms411Z7N3&cid=797315
```

will enter a redirection page, from where visitors will be taken to the correct page.

