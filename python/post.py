import os
import sys
import time
import requests
import json
from requests_oauthlib import OAuth1
MEDIA_ENDPOINT_URL = 'https://upload.twitter.com/1.1/media/upload.json'
POST_TWEET_URL = 'https://api.twitter.com/2/tweets'
# 对应api的密钥
CONSUMER_KEY = 'your_consumer_key'
CONSUMER_SECRET = 'your_consumer_secret'
ACCESS_TOKEN = 'your_access_token'
ACCESS_TOKEN_SECRET = 'your_access_token_secret'  #手动授权操作过后的#下面直接从后台拿现成的

# 视频/图片路径
VIDEO_FILENAME = 'D:\\img\\3.jpg'
# 验证
oauth = OAuth1(CONSUMER_KEY,
               client_secret=CONSUMER_SECRET,
               resource_owner_key=ACCESS_TOKEN,
               resource_owner_secret=ACCESS_TOKEN_SECRET)
class VideoTweet(object):
    """
    自动发推特
    """
    def __init__(self, file_name):
        """
        定义视频推文属性
        """
        self.video_filename = self.get_video(file_name) if "http" in file_name else file_name
        self.total_bytes = os.path.getsize(self.video_filename)
        self.media_id = None
        self.processing_info = None
    def get_video(self, video_url):
        """
        下载视频
        :param video_url:
        :return:
        """
        # 下载视频到本地
        video_filename = 'downloaded_video.mp4'
        # 如果文件已存在，先删除
        if os.path.exists(video_filename):
            os.remove(video_filename)
        response = requests.get(video_url, stream=True)
        with open(video_filename, 'wb') as file:
            for chunk in response.iter_content(chunk_size=1024):
                if chunk:
                    file.write(chunk)
        return video_filename
    def upload_init(self):
        """
        初始化上传
        """
        print('INIT')
        request_data = {
            'command': 'INIT',
            'total_bytes': self.total_bytes,
            #图片参数 需要哪个就注释掉另外一个
            'media_type': 'image/jpeg',  
            'media_category': 'tweet_image' 
            #视频参数
            #'media_type': 'video/mp4',
            #'media_category': 'tweet_video'
        }
        req = requests.post(url=MEDIA_ENDPOINT_URL, data=request_data, auth=oauth)
        print(req.text)
        media_id = req.json()['media_id']
        self.media_id = media_id
        print('Media ID: %s' % str(media_id))
    def upload_append(self):
        """
        以分块方式上传媒体并追加已上传的块
        """
        segment_id = 0
        bytes_sent = 0
        file = open(self.video_filename, 'rb')
        while bytes_sent < self.total_bytes:
            chunk = file.read(4 * 1024 * 1024)
            print('APPEND')
            request_data = {
                'command': 'APPEND',
                'media_id': self.media_id,
                'segment_index': segment_id
            }
            files = {
                'media': chunk
            }
            req = requests.post(url=MEDIA_ENDPOINT_URL, data=request_data, files=files, auth=oauth)
            if req.status_code < 200 or req.status_code > 299:
                print(req.status_code)
                print(req.text)
                sys.exit(0)
            segment_id = segment_id + 1
            bytes_sent = file.tell()
            print('%s of %s bytes uploaded' % (str(bytes_sent), str(self.total_bytes)))
        print('Upload chunks complete.')
    def upload_finalize(self):
        """
        完成上传并开始视频处理
        """
        print('FINALIZE')
        request_data = {
            'command': 'FINALIZE',
            'media_id': self.media_id
        }
        req = requests.post(url=MEDIA_ENDPOINT_URL, data=request_data, auth=oauth)
        print(req.json())
        self.processing_info = req.json().get('processing_info', None)
        self.check_status()
    def check_status(self):
        """
        检查视频处理状态
        """
        if self.processing_info is None:
            return
        state = self.processing_info['state']     
        print('Media processing status is %s ' % state)
        if state == u'succeeded':
            return
        if state == u'failed':
            sys.exit(0)
        check_after_secs = self.processing_info['check_after_secs']
        print('Checking after %s seconds' % str(check_after_secs))
        time.sleep(check_after_secs)
        print('STATUS')
        request_params = {
            'command': 'STATUS',
            'media_id': self.media_id
        }
        req = requests.get(url=MEDIA_ENDPOINT_URL, params=request_params, auth=oauth)
        self.processing_info = req.json().get('processing_info', None)
        self.check_status()
    def tweet(self):
        """
        发布带有附加视频的推文
        """
        # request_data = {
        #     'status': 'I just uploaded a video with the @TwitterAPI.',
        #     'media_ids': self.media_id
        # }
        media_ids = [str(self.media_id)]
        request_data = {
            "text": f'节日快乐!',
            "media": {"media_ids": media_ids}}
        print("请求参数为:", json.dumps(request_data, ensure_ascii=False))
        req = requests.post(url=POST_TWEET_URL, json=request_data, auth=oauth)
        print(req.json())
if __name__ == '__main__':
    # VIDEO_FILENAME 路径 
    videoTweet = VideoTweet(VIDEO_FILENAME)
    videoTweet.upload_init()
    videoTweet.upload_append()
    videoTweet.upload_finalize()
    videoTweet.tweet()

#### 注意事项： 
# 该代码只是示例,需要flask api私聊，有关于媒体多个上传和状态判断细节。
#1.apikey.py会生成一个授权地址，并且需要填写pin,然后会给你一个apikey和secect,apikey.py后结果如下：
#PS F:\xp>  & 'e:\Program Files\Python\python.exe'  'apikey.py' 
#Got OAuth token: xxxxxxxxxxxxxxxxxx
#Please go here and authorize: https://api.twitter.com/oauth/authorize?oauth_token=xxxxxxxx
#Paste the PIN here: 1234567
#Your API key is:  xxxx          secect:  xxxx
#只需要将上面的密钥代替到对应的密钥就行，还有就是要修改VIDEO_FILENAME和upload_init中的request_data中的参数就行，
#如果是图片就把注释弄开，然后把视频参数加上注释，否则反过来。
#视频必须是本地才能上传，如果视频是一个url的，那就进行下载，再上传，该功能已经实现，所以可以直接用，
#图片目前只是试过本地的上传，如果是url的不知道是否需要下载再上传，读者可以自己试一下。  
#2.执行post.py结果如下:
#PS F:\xp>  & 'e:\Program Files\Python\python.exe'  'post.py' 

