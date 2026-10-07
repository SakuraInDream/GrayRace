# GrayRace

## 如何参加测试

可根据自身情况选用任一种方式参加测试。均需要有能够连接 GitHub 的网络环境。

1.**Git** (更新方便)

- 电脑上安装 Git -> https://git-scm.com/install/windows 已有则跳过
- 在游戏根目录的 Mods 文件夹处，打开 Git 控制台(可以在资源管理器空白处右键选择 Open Git Bash here)，然后输入 `git clone --depth 1 https://github.com/SakuraInDream/GrayRace.git`
- 再输入 `git switch 具体分支名` 切换到要测试的分支 (Branch)，当前最新测试分支是 `refactor/vf-turret-pipeline`，测试分支名可能会随后续开发而改变，以仓库存在的具体分支名为准，可输入 `git branch` 查看仓库的所有分支。
- 等待完成即可在 Mods 路径下找到 GrayRace
- 日后要更新，直接进入 GrayRace 文件夹然后打开控制台，输入 `git pull` 即可自动同步更新

> --depth 1 只拉取最后一次提交

2.**使用 GitHub Desktop** (界面友好)

本质还是 Git，但是界面相对更友好一些

- 前往官网 https://github.com/apps/desktop 下载并安装
- 在当前 GitHub 页面找到并点击绿色 Code 按钮，然后点击 Open with GitHub Desktop
- 部分浏览器会弹出一个信息提示是否允许使用 "GitHubDesktop" 打开链接，允许
- 设置路径到游戏根目录的 Mods 文件夹，等待 APP 自动 `clone`
- 切换分支，直接在上方找到按钮点选分支即可
- 日后要更新，直接打开 GitHub Desktop 然后点击上方的 fetch origin 即可

3.**GitHub** 手动下载

- 在当前 GitHub 页面找到绿色的 Code 按钮，点击按钮后找到 Download ZIP 并点击
- 下载完成后解压到游戏根目录的 Mods 文件夹内
- 每次更新都需要手动重新下载一次