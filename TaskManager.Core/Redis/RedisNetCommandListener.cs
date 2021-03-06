﻿using BSF.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using BSF.Extensions;

namespace TaskManager.Core.Redis
{
    /// <summary>
    /// Redis 网络命令监听器
    /// </summary>
    public class RedisNetCommandListener : IDisposable
    {
        
        public string RedisServerIp;
        private CancellationTokenSource cancelSource;
        private RedisDb redisDb;//当前监听连接
        private string channelName;
        private bool isdisposeing = false;//监听释放标记
        public string Name="redis";

        public RedisNetCommandListener(string redisserverip)
        {
            RedisServerIp = redisserverip;
        }

        public void Register(Action<string, string> action,  Action<RedisErrorInfo> errorAction,CancellationTokenSource cancelsource, string channel)
        {
            cancelSource = cancelsource; channelName = channel;
            System.Threading.Tasks.Task.Factory.StartNew(() =>
            {
                NetSubscribe(action, errorAction, channel);//开启及时网络订阅
            }, cancelSource.Token);
        }

        private void NetSubscribe(Action<string, string> action, Action<RedisErrorInfo> errorAction, string channel)
        {
            while (!cancelSource.IsCancellationRequested)
            {
                try
                {
                    try
                    {
                        CloseRedisClient();
                        RedisSubscribe(action, errorAction, channel);
                    }
                    catch (Exception exp)
                    {
                        if (isdisposeing == false)
                        {
                            errorAction.Invoke(new RedisErrorInfo() { Exception = exp, ChannelName = channel, Message = string.Format("MQ心跳redis订阅通信消息出错,请检查redis服务器,订阅名:{0}", Name) });
                        }
                    }
                    System.Threading.Thread.Sleep(RedisConfig.Redis_Subscribe_FailConnect_ReConnect_Every_Time * 1000);
                }
                catch (Exception exp)
                {
                    // when thread is sleeping,but we cancel this thread,may throw thread abort error 
                }
            }
        }

        private void RedisSubscribe(Action<string, string> action, Action<RedisErrorInfo> errorAction, string channelname)
        {
            var manager = new BSF.Redis.RedisManager();
            redisDb = manager.CreateClient(RedisServerIp.Split(':')[0], Convert.ToInt32(RedisServerIp.Split(':')[1]), "");
            using (var subscription = redisDb.GetClient().CreateSubscription())
            {
                subscription.OnSubscribe = channel =>
                {

                    //订阅事件
                };
                subscription.OnUnSubscribe = channel =>
                {

                    //退订事件
                };
                subscription.OnMessage = (channel, msg) =>
                {
                    try
                    {
                        if (msg == "RedisNetCommandListener-Close" || isdisposeing == true)//关闭通道
                        {
                            try { subscription.UnSubscribeFromChannels(channel); }
                            catch { }
                        }
                        else
                        {
                            if (action != null)
                                action.Invoke(channel, msg);
                        }
                    }
                    catch (Exception exp)
                    {
                        errorAction.Invoke(new RedisErrorInfo() { ChannelName= channelname , Exception=exp, Message= string.Format("MQredis订阅通信消息,通道:{1},处理消息{0},订阅名:{2}出错", msg.NullToEmpty(), channelname, Name) });
                    }
                };
                subscription.SubscribeToChannels(channelname);
            }
        }

        private void CloseRedisClient()
        {
            try
            {
                if (redisDb != null)
                {

                    if (redisDb.GetClient() != null)
                    {
                        redisDb.GetClient().Quit();
                    }
                    //redisDb.Dispose();
                    redisDb = null;
                }
            }
            catch (Exception exp)
            { }
        }

        public void Dispose()
        {
            isdisposeing = true;
            try
            {
                try
                {
                    //通知关闭监听通道
                    if (redisDb != null)
                    {
                        var manager = new BSF.Redis.RedisManager();
                        using (var db = manager.CreateClient(RedisServerIp.Split(':')[0], Convert.ToInt32(RedisServerIp.Split(':')[1]), ""))
                        {
                            db.GetClient().PublishMessage(channelName, "RedisNetCommandListener-Close");
                            db.GetClient().Quit();
                        }
                    }
                }
                catch { }
                //关闭当前连接
                CloseRedisClient();
            }
            catch (Exception exp)
            { }
        }
    }
}
