using FluentAssertions;
using SharpDom.Data;
using Xunit;

namespace SharpDom.Tests.Utils
{
    public class StackTests
    {

        [Fact]
        public void IsEmpty_ShouldReturnTrue_IfNoItemsAdded ()
        {
            // Arrange
            var sut = new Stack<string>();
            
            // Act
            var isStackEmpty = sut.IsEmpty();

            // Assert
            isStackEmpty.Should().BeTrue();
        }
        
        [Fact]
        public void Push_ShouldAddItemToStack()
        {
            // Arrange
            var sut = new Stack<string>();

            // Act
            sut.Push("foo");
            var isStackEmpty = sut.IsEmpty();

            // Assert
            isStackEmpty.Should().BeFalse();
        }

        [Fact]
        public void TryPop_ShouldOutputLastInFirstOut()
        {
            // Arrange
            const string itemOne = "foo";
            const string itemTwo = "bar";
            const string itemThree = "baz";
            var sut = new Stack<string>();
            sut.Push(itemOne);
            sut.Push(itemTwo);
            sut.Push(itemThree);

            // Act
            var wasPopOneSuccessful = sut.TryPop(out var popItemOne);
            var wasPopTwoSuccessful = sut.TryPop(out var popItemTwo);
            var wasPopThreeSuccessful = sut.TryPop(out var popItemThree);

            // Assert
            wasPopOneSuccessful.Should().BeTrue();
            wasPopTwoSuccessful.Should().BeTrue();
            wasPopThreeSuccessful.Should().BeTrue();
            popItemOne.Should().BeEquivalentTo(itemThree);
            popItemTwo.Should().BeEquivalentTo(itemTwo);
            popItemThree.Should().BeEquivalentTo(itemOne);
        }

        [Fact]
        public void TryPop_ShouldReturnFalseAndOutputDefault_IfStackIsEmpty()
        {
            // Arrange
            var sut = new Stack<string>();

            // Act
            var wasPopSuccessful = sut.TryPop(out var popItem);

            // Assert
            wasPopSuccessful.Should().BeFalse();
            popItem.Should().Be(default);
        }
        
    }
}