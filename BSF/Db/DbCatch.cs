﻿using BSF.Log;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BSF.Extensions;
using BSF.BaseService.Monitor;
using Microsoft.AspNetCore.Http.Extensions;

namespace BSF.Db
{
    /// <summary>
    /// Db访问拦截 车毅
    /// </summary>
    public class DbCatch
    {
        /// <summary>
        /// 错误拦截
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="istimeWatch"></param>
        /// <param name="sql"></param>
        /// <param name="procedurePar"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        public static T Catch<T>(bool istimeWatch, string datasource, string sql, List<ProcedureParameter> procedurePar, Func<T> action)
        {
            TimeWatchLogInfo info = new TimeWatchLogInfo();
            try
            {
                TimeWatchLog watch = new TimeWatchLog();//网络耗时打印

                try
                {
                    if (procedurePar != null)
                    {

                        string url = (System.Web.HttpContext.Current != null ? (System.Web.HttpContext.Current.Request.GetDisplayUrl().SubString2(90)) : "");
                        info.sqlip = datasource.NullToEmpty();
                        info.msg = sql.NullToEmpty();
                        info.url = url;
                        info.logtag = sql.GetHashCode();
                        info.logtype = EnumTimeWatchLogType.SqlCmd;
                        info.remark = "";
                        foreach (var p in procedurePar)
                        {
                            info.remark += p.Name + ":" + p.Value + ";";
                        }
                    }
                }
                catch
                {

                }
                var r = action.Invoke();
                if (istimeWatch == true)
                {
                    watch.Write(info);
                }
                return r;
            }
            catch (Exception exp)
            {//"sql执行出错" + info.msg
                ErrorLog.Write(new ErrorLogInfo()
                {
                    developer = "",//获取项目默认
                    logtag = "",//获取项目默认
                    msg = "[sql执行]" + info.msg,
                    logtype = BSF.BaseService.Monitor.EnumErrorLogType.CommonError,
                    remark = "[sql参数]" + info.remark.NullToEmpty(),
                }, exp);
                throw exp;
            }
        }
    }
}
