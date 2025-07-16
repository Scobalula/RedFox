using RedFox.Graphics3D;
using RedFox.Graphics3D.Cast;
using RedFox.Graphics3D.SEAnim;
using RedFox.Graphics3D.SEModel;
using RedFox.Graphics3D.Skeletal;
using RedFox.Graphics3D.Translation;

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

            //var document = FBXDocumentReader.ReadDocument(@"D:\Tools\CAT_OF_THOMAS\thingy.fbx");

            ////fbx_defs = elem_find_first(elem_root, b'Definitions')  # can be None
            ////fbx_nodes = elem_find_first(elem_root, b'Objects')
            ////fbx_connections = elem_find_first(elem_root, b'Connections')

            //var definitions = document.Nodes.FirstOrDefault(x => x.Name.Equals("Definitions"));
            //var objects = document.Nodes.FirstOrDefault(x => x.Name.Equals("Objects"));
            //var connections = document.Nodes.FirstOrDefault(x => x.Name.Equals("Connections"));

            //if (definitions is null)
            //    throw new KeyNotFoundException("Failed to locate \"Definitions\" within the FBX file");
            //if (objects is null)
            //    throw new KeyNotFoundException("Failed to locate \"Objects\" within the FBX file");
            //if (connections is null)
            //    throw new KeyNotFoundException("Failed to locate \"Connections\" within the FBX file");

            //var skeleton = new Skeleton("yo boy");

            //// Parse armature
            //var modelNodes = objects.Children.Where(x => x.Name.Equals("Model"));

            //foreach (var modelNode in modelNodes)
            //{
            //    if (!modelNode.Properties[2].Equals("LimbNode"))
            //        continue;

            //    var name = modelNode.Properties[1].Cast<FBXPropertyString>().Value;
            //    var properties = modelNode.Children.First(x => x.Name.Equals("Properties70")).Children.Where(x => x.Name == "P").ToArray();

            //    Console.WriteLine(properties.Length);


            //    Console.WriteLine(modelNode.Properties[1]);
            //}

            ////foreach (var item in document.Nodes)
            ////{
            ////    var objects = item.
            ////    if (item.Name == "Connections")
            ////    {
            ////        foreach (var child in item.Children)
            ////        {
            ////            Console.WriteLine(child.Name);

            ////            foreach (var child2 in child.Properties)
            ////            {
            ////                Console.WriteLine(child2);
            ////            }
            ////        }
            ////    }
            ////}
            ///
            var factory = new Graphics3DTranslatorFactory().WithDefaultTranslators();

            var animation = factory.Load<SkeletonAnimation>(@"C:\AlchemistExample\IW8-MP5\XAnimOut\vm_sm_mpapa5_sprint_loop_grip.cast");

            Console.WriteLine(animation.Tracks[2].TranslationZCurve.Type);
            Console.WriteLine(animation.Tracks[2].TranslationZCurve.Sample(4.5f));


            var xFrame = animation.Tracks[2].ScaleXCurve.Apply(0);

            factory.Save(@"C:\AlchemistExample\IW8-MP5\XAnimOut\vm_sm_mpapa5_sprint_loop_grip2.cast", animation);
        }
    }
}
