using RedFox.Graphics3D;
using RedFox.Graphics3D.Cast;
using RedFox.Graphics3D.KaydaraFBX.Document;
using RedFox.Graphics3D.KaydaraFBX.Reading;
using RedFox.Graphics3D.SEAnim;
using RedFox.Graphics3D.SEModel;
using RedFox.Graphics3D.Skeletal;
using RedFox.Graphics3D.Translation;
using System.Diagnostics;

namespace RedFox.Sandbox.CLI
{
    internal class Program
    {
        static void Main(string[] args)
        {
            ////var factory = new Graphics3DTranslatorFactory().WithDefaultTranslators();

            ////var models = new Model2[]
            ////{
            ////    new(@"D:\Tools\CordyCap\Greyhound\exported_files\modern_warfare_4\xmodels\mp_vm_arms_ger_s4_constance_02_gold\mp_vm_arms_ger_s4_constance_02_gold_LOD0.cast", ModelType.ViewHands),
            ////    new(@"D:\Tools\CordyCap\Greyhound\exported_files\modern_warfare_4\xmodels\attachment_wm_sm_mpapa5_receiver_mp\attachment_wm_sm_mpapa5_receiver_mp_LOD0.cast", ModelType.Attachment)
            ////    {
            ////        ParentBoneTag = "tag_weapon"
            ////    },
            ////    new(@"D:\Tools\CordyCap\Greyhound\exported_files\modern_warfare_4\xmodels\attachment_wm_sm_mpapa5_barrel\attachment_wm_sm_mpapa5_barrel_LOD0.cast", ModelType.Attachment),
            ////    new(@"D:\Tools\CordyCap\Greyhound\exported_files\modern_warfare_4\xmodels\attachment_wm_sm_mpapa5_mag\attachment_wm_sm_mpapa5_mag_LOD0.cast", ModelType.Attachment),
            ////    new(@"D:\Tools\CordyCap\Greyhound\exported_files\modern_warfare_4\xmodels\attachment_wm_sm_mpapa5_stock\attachment_wm_sm_mpapa5_stock_LOD0.cast", ModelType.Attachment),
            ////};

            ////var viewhands = models.First(x => x.Type.Equals(ModelType.ViewHands));
            ////var viewhandsModel = factory.Load<Model>(viewhands.FilePath);

            ////foreach (var attachment in models.Where(x => x.Type == ModelType.Attachment))
            ////{
            ////    var model = factory.Load<Model>(attachment.FilePath);

            ////    if (viewhandsModel.Skeleton is null)
            ////        continue;
            ////    if (model.Skeleton is null)
            ////        continue;
            ////    if (viewhandsModel.Skeleton.Bones.Count <= 0)
            ////        continue;
            ////    if (model.Skeleton.Bones.Count <= 0)
            ////        continue;

            ////    var rootBone = model.Skeleton.Bones.First();
            ////    var newParentBone = viewhandsModel.Skeleton.FindBone(attachment.ParentBoneTag ?? model.Skeleton.Bones.First().Name);

            ////    if (newParentBone is null)
            ////        continue;

            ////    rootBone.Parent = newParentBone;

            ////    foreach (var bone in model.Skeleton.Bones)
            ////    {
            ////        viewhandsModel.Skeleton.Bones.Add(bone);
            ////    }
            ////}

            ////factory.Save("test.semodel", viewhandsModel);

            var document = FBXDocumentReader.ReadDocument(@"D:\Tools\CAT_OF_THOMAS\thingy.fbx");

            ////fbx_defs = elem_find_first(elem_root, b'Definitions')  # can be None
            ////fbx_nodes = elem_find_first(elem_root, b'Objects')
            ////fbx_connections = elem_find_first(elem_root, b'Connections')

            var definitions = document.Nodes.FirstOrDefault(x => x.Name.Equals("Definitions"));
            var objects = document.Nodes.FirstOrDefault(x => x.Name.Equals("Objects"));
            var connections = document.Nodes.FirstOrDefault(x => x.Name.Equals("Connections"));

            if (definitions is null)
                throw new KeyNotFoundException("Failed to locate \"Definitions\" within the FBX file");
            if (objects is null)
                throw new KeyNotFoundException("Failed to locate \"Objects\" within the FBX file");
            if (connections is null)
                throw new KeyNotFoundException("Failed to locate \"Connections\" within the FBX file");

            var skeleton = new Skeleton("yo boy");

            // Parse armature
            var modelNodes = objects.Children.Where(x => x.Name.Equals("Model"));

            foreach (var modelNode in modelNodes)
            {
                if (!modelNode.Properties[2].Equals("LimbNode"))
                    continue;

                var name = modelNode.Properties[1].Cast<FBXPropertyString>().Value;
                var properties = modelNode.Children.First(x => x.Name.Equals("Properties70")).Children.Where(x => x.Name == "P").ToArray();

                foreach (var property in properties)
                {
                    Console.WriteLine(property.Properties[0]);
                }

                //Console.WriteLine(properties.Length);


                //Console.WriteLine(modelNode.Properties[1]);


            }

            ////// Consume constrains
            ////var constraint

            //////foreach (var item in document.Nodes)
            //////{
            //////    var objects = item.
            //////    if (item.Name == "Connections")
            //////    {
            //////        foreach (var child in item.Children)
            //////        {
            //////            Console.WriteLine(child.Name);

            //////            foreach (var child2 in child.Properties)
            //////            {
            //////                Console.WriteLine(child2);
            //////            }
            //////        }
            //////    }
            //////}
            /////
            //var factory = new Graphics3DTranslatorFactory().WithDefaultTranslators();

            //var model = factory.Load<Model>(@"C:\shit\CAST Models\mp_vm_arms_fender_iw9_8_1_LOD0.cast");
            //var animation = factory.Load<SkeletonAnimation>(@"C:\shit\CAST Animations\vm_p20_dm_sa700_reload_xmag.cast");

            //var sampler = new SkeletonAnimationSampler("name", animation, model.Skeleton!);
            //var newAnimation = new SkeletonAnimation(animation.Name)
            //{
            //    Framerate = 30.0f
            //};
            //var (minFrame, maxFrame) = animation.GetAnimationFrameRange();

            //foreach (var bone in model.Skeleton.Bones)
            //{
            //    newAnimation.Tracks.Add(new SkeletonAnimationTrack(bone.Name)
            //    {
            //        TranslationCurve = new(TransformSpace.Local, TransformType.Absolute),
            //        RotationCurve = new(TransformSpace.Local, TransformType.Absolute),
            //    });
            //}

            //for (float i = minFrame; i < maxFrame; i += 1)
            //{
            //    model.Skeleton.Bones.ForEach(x => x.ActiveTransform.Invalidate());
            //    sampler.Update(i, AnimationSampleType.AbsoluteFrameTime);

            //    for (int b = 0; b < model.Skeleton.Bones.Count; b++)
            //    {
            //        //if (model.Skeleton.Bones[b].Name == "tag_ads")
            //        //    Debugger.Break();
            //        newAnimation.Tracks[b].AddTranslationFrame(i, model.Skeleton.Bones[b].GetActiveWorldPosition());
            //        newAnimation.Tracks[b].AddRotationFrame(i, model.Skeleton.Bones[b].GetActiveWorldRotation());
            //    }
            //}


            //foreach (var bone in model.Skeleton.Bones)
            //{
            //    var currentA = bone.GetBaseWorldPosition();
            //    var currentB = bone.GetBaseWorldRotation();

            //    bone.MoveTo(null);

            //    bone.BaseTransform.LocalPosition = currentA;
            //    bone.BaseTransform.LocalRotation = currentB;
            //}

            ////Console.WriteLine(animation.Tracks[2].TranslationZCurve.Type);
            ////Console.WriteLine(animation.Tracks[2].TranslationZCurve.Sample(4.5f));


            //////var xFrame = animation.Tracks[2].ScaleXCurve.Apply(0);

            //factory.Save(@"C:\shit\CAST Models\mp_vm_arms_fender_iw9_8_1_LOD02.cast", model);
            //factory.Save(@"C:\shit\CAST Models\mp_vm_arms_fender_iw9_8_1_LOD03.cast", newAnimation);

            //var finaler = factory.Load<SkeletonAnimation>(@"C:\shit\CAST Models\mp_vm_arms_fender_iw9_8_1_LOD03.cast");
            //var thingy = finaler.GetAnimationFrameRange();
            //Console.WriteLine(finaler);
        }
    }
}
