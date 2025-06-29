using RedFox.Graphics3D;
using RedFox.Graphics3D.Cast;
using RedFox.Graphics3D.SEAnim;
using RedFox.Graphics3D.SEModel;
using RedFox.Graphics3D.Translation;

namespace RedFox.Sandbox.CLI
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var factory = new Graphics3DTranslatorFactory().WithDefaultTranslators();

            var models = new Model2[]
            {
                new(@"D:\Tools\CordyCap\Greyhound\exported_files\modern_warfare_4\xmodels\mp_vm_arms_ger_s4_constance_02_gold\mp_vm_arms_ger_s4_constance_02_gold_LOD0.cast", ModelType.ViewHands),
                new(@"D:\Tools\CordyCap\Greyhound\exported_files\modern_warfare_4\xmodels\attachment_wm_sm_mpapa5_receiver_mp\attachment_wm_sm_mpapa5_receiver_mp_LOD0.cast", ModelType.Attachment)
                {
                    ParentBoneTag = "tag_weapon"
                },
                new(@"D:\Tools\CordyCap\Greyhound\exported_files\modern_warfare_4\xmodels\attachment_wm_sm_mpapa5_barrel\attachment_wm_sm_mpapa5_barrel_LOD0.cast", ModelType.Attachment),
                new(@"D:\Tools\CordyCap\Greyhound\exported_files\modern_warfare_4\xmodels\attachment_wm_sm_mpapa5_mag\attachment_wm_sm_mpapa5_mag_LOD0.cast", ModelType.Attachment),
                new(@"D:\Tools\CordyCap\Greyhound\exported_files\modern_warfare_4\xmodels\attachment_wm_sm_mpapa5_stock\attachment_wm_sm_mpapa5_stock_LOD0.cast", ModelType.Attachment),
            };

            var viewhands = models.First(x => x.Type.Equals(ModelType.ViewHands));
            var viewhandsModel = factory.Load<Model>(viewhands.FilePath);

            foreach (var attachment in models.Where(x => x.Type == ModelType.Attachment))
            {
                var model = factory.Load<Model>(attachment.FilePath);

                if (viewhandsModel.Skeleton is null)
                    continue;
                if (model.Skeleton is null)
                    continue;
                if (viewhandsModel.Skeleton.Bones.Count <= 0)
                    continue;
                if (model.Skeleton.Bones.Count <= 0)
                    continue;

                var rootBone = model.Skeleton.Bones.First();
                var newParentBone = viewhandsModel.Skeleton.FindBone(attachment.ParentBoneTag ?? model.Skeleton.Bones.First().Name);

                if (newParentBone is null)
                    continue;

                rootBone.Parent = newParentBone;

                foreach (var bone in model.Skeleton.Bones)
                {
                    viewhandsModel.Skeleton.Bones.Add(bone);
                }
            }

            factory.Save("test.semodel", viewhandsModel);
        }
    }
}
