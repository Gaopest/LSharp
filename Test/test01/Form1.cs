﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace test01
{
    public partial class Form1 : Form, CLRSharp.ICLRSharp_Logger
    {
        public static Form1 gthis = null;
        public Form1()
        {
            InitializeComponent();
            gthis = this;
        }
        CLRSharp.CLRSharp_Environment env;
        private void Form1_Load(object sender, EventArgs e)
        {

            //创建CLRSharp环境
            env = new CLRSharp.CLRSharp_Environment(this);

            //加载L#模块
            var dll = System.IO.File.ReadAllBytes("testdll01.dll");
            var pdb = System.IO.File.ReadAllBytes("testdll01.pdb");

            System.IO.MemoryStream msDll = new System.IO.MemoryStream(dll);
            System.IO.MemoryStream msPdb = new System.IO.MemoryStream(pdb);
            //env.LoadModule (msDll);//不需要pdb的话
            bool useSystemAssm = false;
            if (useSystemAssm)
            {
                byte[] dllbytes = msDll.ToArray();
                byte[] pdbbytes = msPdb.ToArray();
                var assem = System.Reflection.Assembly.Load(dllbytes, pdbbytes);//用系统反射加载
                env.AddSerachAssembly(assem);//直接搜索系统反射，此时L#不发挥作用，为了调试的功能
            }
            else
            {
                env.LoadModule(msDll, msPdb, new Mono.Cecil.Pdb.PdbReaderProvider());
            }
            Log("LoadModule HotFixCode.dll done.");
            env.RegCrossBind(new MyCrossBind());

            //step01建立一个线程上下文，用来模拟L#的线程模型，每个线程创建一个即可。
            CLRSharp.ThreadContext context = new CLRSharp.ThreadContext(env);
            context.SetNoTry = true;
            Log("Create ThreadContext for L#.");

            //step02取得想要调用的L#类型
            CLRSharp.ICLRType wantType = env.GetType("HoxFixCode.TestClass");//用全名称，包括命名空间
            Log("GetType:" + wantType.Name);
            //和反射代码中的Type.GetType相对应

            //step03 静态调用
            //得到类型上的一个函数，第一个参数是函数名字，第二个参数是函数的参数表，这是一个没有参数的函数
            CLRSharp.IMethod method01 = wantType.GetMethod("Test1", CLRSharp.MethodParamList.constEmpty());
            method01.Invoke(context, null, null);//第三个参数是object[] 参数表，这个例子不需要参数
            //这是个静态函数调用，对应到代码他就是HotFixCode.TestClass.Test1();

            //step04 成员调用
            //第二个测试程序是一个成员变量，所以先要创建实例
            //CLRSharp.CLRSharp_Instance typeObj = new CLRSharp.CLRSharp_Instance(wantType as CLRSharp.ICLRType_Sharp);//创建实例
            //上一句写的有问题，执行构造函数返回的才是 new出来的对象
            CLRSharp.IMethod methodctor = wantType.GetMethod(".ctor", CLRSharp.MethodParamList.constEmpty());//取得构造函数
            //这里用object 就可以脚本和反射通用了
            object typeObj = methodctor.Invoke(context, null, null);//执行构造函数

            //这几行的作用对应到代码就约等于 HotFixCode.TestClass typeObj =new HotFixCode.TestClass();
            CLRSharp.IMethod method02 = wantType.GetMethod("Test2", CLRSharp.MethodParamList.constEmpty());
            for (int i = 0; i < 5; i++)
            {
                method02.Invoke(context, typeObj, null);
            }
            //这两行的作用就相当于 typeOBj.Test2();

            //请注意，不要在初始化之后，再修改ParamList的内容
            CLRSharp.MethodParamList list = CLRSharp.MethodParamList.Make(
         
                        env.GetType(typeof(int)),
                        env.GetType(typeof(string))
                   
                );
            CLRSharp.IMethod method03 = wantType.GetMethod("Test3", list);
            CallMethod(method03, typeObj, 345, "abbc");
        }

        void CallMethod(CLRSharp.IMethod method,object _this,params object[] _params)
        {
            CLRSharp.ThreadContext context=CLRSharp.ThreadContext.activeContext;
            method.Invoke(context, _this, _params);
        }

        public void Log(string str)
        {
            listBox1.Items.Add(str);
        }

        public void Log_Warning(string str)
        {
            listBox1.Items.Add("<W>" + str);
        }

        public void Log_Error(string str)
        {
            listBox1.Items.Add("<E>" + str);
        }
    }


    public static class Interface
    {

        public static void Test1()
        {
            Form1.gthis.Log("这是真的");
        }
        public static void Test2()
        {
            Form1.gthis.Log("这不是梦");
        }


        //接口也是一种常用形态，但是基于脚本不能动态产生类型的绝对前提
        //脚本产生程序可用的接口似乎是不可能的任务，但是，在我们这里，并非不可能
        public interface IMyType
        {
            string GetName();
            string GetDesc();

            void SetName(string name);
        }

        //SetMyType，UseType 直接使用程序接口，但是这个接口实例从脚本中返回
        //这实质是不可能的，要通过CrossBind去转发
        static IMyType __type = null;
        public static void SetMyType(IMyType _type)
        {
            __type = _type;
        }
        public static void UseType()
        {
            if(__type!=null)
            {
                Form1.gthis.Log(__type.GetName() + ":" + __type.GetDesc());
            }
        }
    }
}
